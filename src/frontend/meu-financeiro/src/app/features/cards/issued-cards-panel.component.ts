import { Component, Input, OnChanges, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonDirective } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { Tooltip } from 'primeng/tooltip';
import { ConfirmationService } from 'primeng/api';
import { AuthService } from '../../core/services/auth.service';
import { IssuedCardService } from '../../core/services/issued-card.service';
import { CreditCard } from '../../core/models/card.model';
import { IssuedCard, IssuedCardKind, IssuedCardRequest, IssuedCardStatus } from '../../core/models/issued-card.model';
import { businessRuleOf } from '../../core/services/problem.util';
import { formatExpiry, maskedNumber, onlyDigits, parseExpiry, splitCardNumber } from '../../core/services/card-number.util';
import { bandeiraLabel } from '../../core/data/bandeiras';

type Mode = 'novo' | 'editar' | 'substituir';

/**
 * Cartões de um grupo (a fatura): titular, adicionais e virtuais, com substituição e cancelamento.
 * O número completo nunca entra em estado nenhum daqui: colar o número inteiro num dos campos o
 * divide na hora e descarta o meio (core/services/card-number.util.ts).
 */
@Component({
  selector: 'mf-issued-cards-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonDirective, InputText, Select, Tooltip],
  template: `
    <div class="mf-panel mf-issued-panel">
      <div class="mf-panel-header">
        <b>Cartões de {{ group.name }}</b>
        @if (group.brand) {
          <span class="mf-sub">{{ brandLabel(group.brand) }}</span>
        }
        @if (authService.canWrite() && !mode()) {
          <button pButton type="button" size="small" [text]="true" class="mf-push" (click)="openNew()">+ Adicionar cartão</button>
        }
      </div>
      <div class="mf-panel-body">
        @if (mode()) {
          <div class="mf-issued-form">
            <div class="mf-issued-form-title">
              {{ mode() === 'novo' ? 'Novo cartão' : mode() === 'editar' ? 'Editar cartão' : 'Substituir o cartão final ' + replacing?.lastFour }}
            </div>
            <div class="mf-issued-grid">
              <div>
                <label class="mf-field-label">Tipo</label>
                <p-select size="small" class="mf-field-input" [options]="kindOptions" optionLabel="label" optionValue="value" [(ngModel)]="form.kind" />
              </div>
              <div>
                <label class="mf-field-label">Primeiros dígitos</label>
                <input pInputText pSize="small" class="mf-field-input" inputmode="numeric" placeholder="opcional, até 6"
                       [(ngModel)]="form.firstDigits" (paste)="onPaste($event)" (ngModelChange)="onFirstDigits($event)" />
              </div>
              <div>
                <label class="mf-field-label">4 últimos</label>
                <input pInputText pSize="small" class="mf-field-input" inputmode="numeric" placeholder="1234"
                       [(ngModel)]="form.lastFour" (paste)="onPaste($event)" (ngModelChange)="onLastFour($event)" />
              </div>
              <div>
                <label class="mf-field-label">Validade</label>
                <input pInputText pSize="small" class="mf-field-input" placeholder="MM/AA" maxlength="7" [(ngModel)]="form.expiry" />
              </div>
              <div>
                <label class="mf-field-label">Nome impresso</label>
                <input pInputText pSize="small" class="mf-field-input" maxlength="60" [(ngModel)]="form.holderName" />
              </div>
            </div>
            @if (splitNote()) {
              <div class="mf-sub mf-issued-note">{{ splitNote() }}</div>
            }
            @if (formError()) {
              <div class="mf-form-error">{{ formError() }}</div>
            }
            <div class="mf-issued-actions">
              <button pButton type="button" size="small" [disabled]="saving()" (click)="save()">Salvar</button>
              <button pButton type="button" size="small" [outlined]="true" (click)="closeForm()">Cancelar</button>
            </div>
          </div>
        }

        @if (loading()) {
          <div class="mf-sub">Carregando…</div>
        } @else if (cards().length === 0) {
          <div class="mf-sub mf-issued-empty">
            Nenhum cartão cadastrado neste grupo. Cadastre o titular, os adicionais e os virtuais — o final de cada um é o
            que a fatura mostra em cada compra.
          </div>
        } @else {
          <table class="mf-issued-table">
            <thead>
              <tr>
                <th>Tipo</th>
                <th>Número</th>
                <th>Validade</th>
                <th>Nome impresso</th>
                <th>Situação</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (c of cards(); track c.id) {
                <tr [class.mf-issued-inactive]="c.status !== IssuedCardStatus.Ativo">
                  <td>{{ kindLabel(c.kind) }}</td>
                  <td class="mf-tabular">
                    {{ masked(c) }}
                    @if (c.brandMismatch) {
                      <i class="pi pi-exclamation-triangle mf-warn"
                         [pTooltip]="'Os primeiros dígitos indicam ' + brandLabel(c.suggestedBrand) + ', mas o grupo está como ' + brandLabel(group.brand) + '. Confira.'"
                         tooltipPosition="top" aria-label="Bandeira diferente da do grupo"></i>
                    }
                  </td>
                  <td class="mf-tabular">{{ expiry(c) || '—' }}</td>
                  <td>{{ c.holderName || '—' }}</td>
                  <td>
                    <span class="mf-badge" [ngClass]="statusClass(c)">{{ statusLabel(c) }}</span>
                  </td>
                  <td class="mf-col-end mf-issued-row-actions">
                    @if (authService.canWrite()) {
                      <button type="button" class="mf-icon-btn" pTooltip="Editar" tooltipPosition="top" aria-label="Editar" (click)="openEdit(c)">
                        <i class="pi pi-pencil"></i>
                      </button>
                      @if (c.status === IssuedCardStatus.Ativo) {
                        <button type="button" class="mf-icon-btn" pTooltip="Substituir (renovação, perda)" tooltipPosition="top" aria-label="Substituir" (click)="openReplace(c)">
                          <i class="pi pi-sync"></i>
                        </button>
                        <button type="button" class="mf-icon-btn" pTooltip="Cancelar" tooltipPosition="top" aria-label="Cancelar cartão" (click)="confirmCancel(c)">
                          <i class="pi pi-ban"></i>
                        </button>
                      }
                      <button type="button" class="mf-icon-btn mf-icon-danger" pTooltip="Excluir" tooltipPosition="top" aria-label="Excluir" (click)="confirmDelete(c)">
                        <i class="pi pi-trash"></i>
                      </button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
        @if (error()) {
          <div class="mf-form-error">{{ error() }}</div>
        }
      </div>
    </div>
  `,
  styles: [
    `
      .mf-issued-panel { margin-top: 1rem; background: var(--mf-card); border: 1px solid var(--mf-line); border-radius: var(--mf-radius); box-shadow: var(--mf-shadow); overflow: hidden; }
      .mf-issued-panel .mf-panel-header { display: flex; align-items: center; gap: 0.75rem; padding: 0.125rem 1rem; min-height: 1.875rem; border-bottom: 1px solid var(--mf-line); }
      .mf-push { margin-left: auto; }
      .mf-issued-form { border: 1px solid var(--mf-line); border-radius: var(--mf-radius); padding: 0.75rem; margin: 0.5rem 0 0.75rem; }
      .mf-issued-form-title { font-weight: 600; margin-bottom: 0.5rem; }
      .mf-issued-grid { display: grid; grid-template-columns: 1fr 1fr 0.8fr 0.7fr 1.6fr; gap: 0.75rem; align-items: end; }
      .mf-issued-actions { display: flex; gap: 0.5rem; margin-top: 0.75rem; }
      .mf-issued-note { margin-top: 0.4rem; }
      .mf-issued-empty { padding: 0.5rem 0; }
      .mf-issued-table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
      .mf-issued-table th { text-align: left; font-weight: 600; padding: 0.3rem 0.5rem; border-bottom: 1px solid var(--mf-line); }
      .mf-issued-table td { padding: 0.35rem 0.5rem; border-bottom: 1px solid var(--mf-line); }
      .mf-issued-inactive td { color: var(--mf-muted); }
      .mf-tabular { font-variant-numeric: tabular-nums; }
      .mf-warn { color: var(--mf-yellow); margin-left: 0.35rem; }
      .mf-col-end { text-align: right; }
      .mf-issued-row-actions { white-space: nowrap; }
      .mf-icon-btn { border: none; background: none; cursor: pointer; color: var(--mf-primary); padding: 0 0.3rem; font-size: 0.95rem; }
      .mf-icon-danger { color: var(--mf-red); }
      .mf-icon-btn:hover { opacity: 0.7; }
      .mf-form-error { color: var(--mf-red); font-size: 0.85rem; margin-top: 0.4rem; }
      @media (max-width: 900px) { .mf-issued-grid { grid-template-columns: 1fr 1fr; } }
    `,
  ],
})
export class IssuedCardsPanelComponent implements OnChanges {
  @Input({ required: true }) group!: CreditCard;

  readonly IssuedCardStatus = IssuedCardStatus;
  readonly kindOptions = [
    { label: 'Titular', value: IssuedCardKind.Titular },
    { label: 'Adicional', value: IssuedCardKind.Adicional },
    { label: 'Virtual', value: IssuedCardKind.Online },
  ];

  readonly cards = signal<IssuedCard[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly mode = signal<Mode | null>(null);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  /** Aviso de que um número completo colado foi dividido e o meio descartado. */
  readonly splitNote = signal<string | null>(null);

  form = AnyForm.empty();
  private editingId: string | null = null;
  replacing: IssuedCard | null = null;

  constructor(
    private readonly service: IssuedCardService,
    private readonly confirmation: ConfirmationService,
    readonly authService: AuthService,
  ) {}

  ngOnChanges(): void {
    this.closeForm();
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.service.getByGroup(this.group.id).subscribe({
      next: (cards) => {
        this.cards.set(cards);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Não foi possível carregar os cartões deste grupo.');
      },
    });
  }

  openNew(): void {
    this.editingId = null;
    this.replacing = null;
    // O primeiro cartão do grupo costuma ser o do titular; os seguintes, adicionais.
    this.form = AnyForm.empty(this.cards().length === 0 ? IssuedCardKind.Titular : IssuedCardKind.Adicional);
    this.openForm('novo');
  }

  openEdit(c: IssuedCard): void {
    this.editingId = c.id;
    this.replacing = null;
    this.form = {
      kind: c.kind,
      firstDigits: c.firstDigits ?? '',
      lastFour: c.lastFour,
      expiry: formatExpiry(c.expiryMonth, c.expiryYear),
      holderName: c.holderName ?? '',
    };
    this.openForm('editar');
  }

  /** Renovação mantém tipo e nome; número e validade são do cartão novo. */
  openReplace(c: IssuedCard): void {
    this.editingId = null;
    this.replacing = c;
    this.form = { ...AnyForm.empty(c.kind), firstDigits: c.firstDigits ?? '', lastFour: c.lastFour, holderName: c.holderName ?? '' };
    this.openForm('substituir');
  }

  private openForm(mode: Mode): void {
    this.formError.set(null);
    this.splitNote.set(null);
    this.mode.set(mode);
  }

  closeForm(): void {
    this.mode.set(null);
    this.replacing = null;
    this.editingId = null;
  }

  /** Número inteiro colado em qualquer um dos dois campos: divide e descarta o meio. */
  onPaste(event: ClipboardEvent): void {
    const parts = splitCardNumber(event.clipboardData?.getData('text'));
    if (!parts) return;
    event.preventDefault();
    this.form.firstDigits = parts.firstDigits;
    this.form.lastFour = parts.lastFour;
    this.splitNote.set('Número completo dividido: ficam só os 6 primeiros e os 4 últimos. O meio não é guardado.');
  }

  // Digitação (ou autocompletar do navegador) com o número inteiro também é dividida.
  onFirstDigits(value: string): void {
    const parts = splitCardNumber(value);
    if (parts) {
      this.form.firstDigits = parts.firstDigits;
      this.form.lastFour = parts.lastFour;
      this.splitNote.set('Número completo dividido: ficam só os 6 primeiros e os 4 últimos. O meio não é guardado.');
      return;
    }
    this.form.firstDigits = onlyDigits(value, 6);
  }

  onLastFour(value: string): void {
    const parts = splitCardNumber(value);
    if (parts) {
      this.form.firstDigits = parts.firstDigits;
      this.form.lastFour = parts.lastFour;
      this.splitNote.set('Número completo dividido: ficam só os 6 primeiros e os 4 últimos. O meio não é guardado.');
      return;
    }
    this.form.lastFour = onlyDigits(value, 4);
  }

  save(): void {
    const expiry = parseExpiry(this.form.expiry);
    if (expiry === 'invalid') {
      this.formError.set('Validade inválida: use MM/AA, com o mês de 01 a 12.');
      return;
    }
    if (this.form.lastFour.length !== 4) {
      this.formError.set('Informe os 4 últimos dígitos do cartão.');
      return;
    }

    const request: IssuedCardRequest = {
      kind: this.form.kind,
      lastFour: this.form.lastFour,
      firstDigits: this.form.firstDigits || null,
      expiryMonth: expiry?.month ?? null,
      expiryYear: expiry?.year ?? null,
      holderName: this.form.holderName.trim() || null,
    };

    const mode = this.mode();
    const request$ =
      mode === 'editar' && this.editingId
        ? this.service.update(this.editingId, request)
        : mode === 'substituir' && this.replacing
          ? this.service.replace(this.replacing.id, request)
          : this.service.create(this.group.id, request);

    this.saving.set(true);
    this.formError.set(null);
    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.closeForm();
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(businessRuleOf(err)?.message ?? 'Não foi possível salvar o cartão.');
      },
    });
  }

  confirmCancel(c: IssuedCard): void {
    this.confirmation.confirm({
      header: 'Cancelar cartão',
      message: `Cancelar o cartão final ${c.lastFour}? Ele continua aparecendo nas faturas antigas.`,
      acceptLabel: 'Cancelar cartão',
      rejectLabel: 'Voltar',
      accept: () => this.service.cancel(c.id).subscribe({ next: () => this.load(), error: (e) => this.showError(e) }),
    });
  }

  confirmDelete(c: IssuedCard): void {
    this.confirmation.confirm({
      header: 'Excluir cartão',
      message: `Excluir o cartão final ${c.lastFour}? Para um cartão que deixou de valer, prefira cancelar.`,
      acceptLabel: 'Excluir',
      rejectLabel: 'Voltar',
      accept: () => this.service.delete(c.id).subscribe({ next: () => this.load(), error: (e) => this.showError(e) }),
    });
  }

  private showError(err: unknown): void {
    this.error.set(businessRuleOf(err)?.message ?? 'Não foi possível concluir a operação.');
  }

  masked(c: IssuedCard): string {
    return maskedNumber(c.firstDigits, c.lastFour);
  }

  expiry(c: IssuedCard): string {
    return formatExpiry(c.expiryMonth, c.expiryYear);
  }

  kindLabel(kind: IssuedCardKind): string {
    return this.kindOptions.find((k) => k.value === kind)?.label ?? '';
  }

  statusLabel(c: IssuedCard): string {
    if (c.status === IssuedCardStatus.Substituido) return 'Substituído';
    if (c.status === IssuedCardStatus.Cancelado) return 'Cancelado';
    return c.isExpired ? 'Vencido' : 'Ativo';
  }

  statusClass(c: IssuedCard): string {
    if (c.status === IssuedCardStatus.Ativo && !c.isExpired) return 'mf-badge-ok';
    if (c.status === IssuedCardStatus.Cancelado) return 'mf-badge-bad';
    return 'mf-badge-neutral';
  }

  brandLabel(value: string | null | undefined): string {
    return bandeiraLabel(value);
  }
}

interface IssuedCardForm {
  kind: IssuedCardKind;
  firstDigits: string;
  lastFour: string;
  expiry: string;
  holderName: string;
}

const AnyForm = {
  empty(kind: IssuedCardKind = IssuedCardKind.Titular): IssuedCardForm {
    return { kind, firstDigits: '', lastFour: '', expiry: '', holderName: '' };
  },
};
