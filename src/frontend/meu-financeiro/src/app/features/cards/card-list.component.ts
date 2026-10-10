import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { ButtonDirective } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Tooltip } from 'primeng/tooltip';
import { AuthService } from '../../core/services/auth.service';
import { CardService } from '../../core/services/card.service';
import { AccountService } from '../../core/services/account.service';
import { Account } from '../../core/models/account.model';
import { InvoiceService } from '../../core/services/invoice.service';
import { CreateCardRequest, CreditCard } from '../../core/models/card.model';
import { CardInvoiceStatus } from '../../core/models/invoice.model';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { PaginationComponent } from '../../shared/components/pagination.component';
import { StatCardComponent } from '../../shared/components/stat-card.component';
import { onlyDigits, parseExpiry, splitCardNumber } from '../../core/services/card-number.util';
import { nextSort, SortDir } from '../../core/services/sort.util';
import { businessRuleOf } from '../../core/services/problem.util';
import { Select } from 'primeng/select';
import { BANDEIRAS_CREDITO, bandeiraLabel, sugerirBandeiraPeloNome } from '../../core/data/bandeiras';
import { IssuedCardsPanelComponent } from './issued-cards-panel.component';
import { IssuedCardKind } from '../../core/models/issued-card.model';

/** Tela de cartões (RF003), espelhando docs/screens/cartoes/mockup.html. */
@Component({
  selector: 'mf-card-list',
  standalone: true,
  imports: [
    Select,
    IssuedCardsPanelComponent,
    CommonModule,
    FormsModule,
    MfIconComponent,
    ButtonDirective,
    InputText,
    ListStateComponent,
    PaginationComponent,
    StatCardComponent,
    ConfirmDialog,
    Tooltip,
  ],
  providers: [ConfirmationService],
  templateUrl: './card-list.component.html',
  styleUrl: './card-list.component.scss',
})
export class CardListComponent implements OnInit {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly cards = signal<CreditCard[]>([]);
  readonly page = signal(1);
  readonly totalPages = signal(0);
  readonly totalItems = signal(0);

  // KPIs (docs/screens/cartoes): busca à parte, sem paginação.
  readonly totalLimit = signal(0);
  readonly openInvoicesTotal = signal(0);

  readonly showForm = signal(false);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly newCard: CreateCardRequest = { name: '', brand: undefined, closingDay: 1, dueDay: 10, limit: undefined };
  readonly brandOptions = [...BANDEIRAS_CREDITO];
  readonly brandLabel = bandeiraLabel;
  /**
   * Ordenação dos blocos, pelos mesmos campos das colunas de antes — no servidor, como nas outras
   * listas. A direção vem de nextSort (clicar de novo inverte).
   */
  sortBy = 'name';
  sortDir: SortDir = 'asc';
  readonly sortFields = [
    { field: 'name', label: 'Nome' },
    { field: 'brand', label: 'Bandeira' },
    { field: 'closingDay', label: 'Fechamento' },
    { field: 'dueDay', label: 'Vencimento' },
    { field: 'limit', label: 'Limite' },
  ];

  sortByField(field: string): void {
    const next = nextSort({ sortBy: this.sortBy, sortDir: this.sortDir }, field, 'name');
    this.sortBy = next.sortBy;
    this.sortDir = next.sortDir;
    this.page.set(1);
    this.load();
  }

  sortIcon(field: string): string {
    if (this.sortBy !== field) return 'pi-sort-alt';
    return this.sortDir === 'asc' ? 'pi-sort-amount-up-alt' : 'pi-sort-amount-down';
  }

  /** Cartão do titular criado junto com o grupo (opcional). Nunca guarda o número inteiro. */
  titular = { firstDigits: '', lastFour: '', expiry: '', holderName: '', accountId: null as string | null };
  readonly titularNote = signal<string | null>(null);
  /** Contas que podem receber o débito de um cartão do grupo (cartão múltiplo). */
  readonly accounts = signal<Account[]>([]);
  private editingIsActive = true;

  get availableLimit(): number {
    return this.totalLimit() - this.openInvoicesTotal();
  }

  constructor(
    private readonly accountService: AccountService,
    private readonly cardService: CardService,
    private readonly invoiceService: InvoiceService,
    private readonly confirmationService: ConfirmationService,
    readonly authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.accountService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => this.accounts.set(r.items.filter((a) => a.isActive)));
    this.loadSummary();
    this.load();
  }

  private loadSummary(): void {
    this.cardService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => {
      this.totalLimit.set(r.items.reduce((sum, c) => sum + (c.limit ?? 0), 0));
    });
    this.invoiceService.getAll({ page: 1, pageSize: 100, status: CardInvoiceStatus.Aberta }).subscribe((r) => {
      this.openInvoicesTotal.set(r.items.reduce((sum, i) => sum + i.totalAmount, 0));
    });
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.cardService.getAll({ page: this.page(), pageSize: 10, sortBy: this.sortBy, sortDir: this.sortDir }).subscribe({
      next: (result) => {
        this.cards.set(result.items);
        this.totalPages.set(result.totalPages);
        this.totalItems.set(result.totalItems);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar os cartões.');
        this.loading.set(false);
      },
    });
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  openForm(): void {
    this.titular = { firstDigits: '', lastFour: '', expiry: '', holderName: '', accountId: null };
    this.titularNote.set(null);
    this.editingId.set(null);
    this.newCard.name = '';
    this.newCard.brand = undefined;
    this.newCard.closingDay = 1;
    this.newCard.dueDay = 10;
    this.newCard.limit = undefined;
    this.showForm.set(true);
    this.formError.set(null);
  }

  edit(card: CreditCard): void {
    this.editingId.set(card.id);
    this.editingIsActive = card.isActive;
    this.newCard.name = card.name;
    this.newCard.brand = card.brand ?? undefined;
    this.newCard.closingDay = card.closingDay;
    this.newCard.dueDay = card.dueDay;
    this.newCard.limit = card.limit;
    this.showForm.set(true);
    this.formError.set(null);
  }

  closeForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
  }

  submit(): void {
    if (!this.newCard.name.trim()) {
      this.formError.set('Informe o nome do cartão.');
      return;
    }

    this.saving.set(true);
    this.formError.set(null);

    const editingId = this.editingId();

    // Titular só na criação, e só se o final foi informado; o resto é opcional.
    let titular: CreateCardRequest['titular'] = null;
    if (!editingId && this.titular.lastFour) {
      const expiry = parseExpiry(this.titular.expiry);
      if (expiry === 'invalid') {
        this.saving.set(false);
        this.formError.set('Validade do titular inválida: use MM/AA.');
        return;
      }
      if (this.titular.lastFour.length !== 4) {
        this.saving.set(false);
        this.formError.set('Informe os 4 últimos dígitos do cartão do titular — ou deixe em branco.');
        return;
      }
      titular = {
        kind: IssuedCardKind.Titular,
        lastFour: this.titular.lastFour,
        firstDigits: this.titular.firstDigits || null,
        expiryMonth: expiry?.month ?? null,
        expiryYear: expiry?.year ?? null,
        holderName: this.titular.holderName.trim() || null,
        accountId: this.titular.accountId,
      };
    }

    const request$ = editingId
      ? this.cardService.update(editingId, { ...this.newCard, isActive: this.editingIsActive })
      : this.cardService.create({ ...this.newCard, titular });

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.editingId.set(null);
        this.newCard.name = '';
        this.newCard.brand = undefined;
        this.load();
        this.loadSummary();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(
          businessRuleOf(err)?.message ??
            (editingId ? 'Não foi possível salvar as alterações.' : 'Não foi possível salvar o cartão.'),
        );
      },
    });
  }

  /** Número inteiro colado no titular: ficam 6 + 4, o meio é descartado (card-number.util). */
  onTitularPaste(event: ClipboardEvent): void {
    const parts = splitCardNumber(event.clipboardData?.getData('text'));
    if (!parts) return;
    event.preventDefault();
    this.applyTitularParts(parts);
  }

  onTitularFirst(value: string): void {
    const parts = splitCardNumber(value);
    if (parts) this.applyTitularParts(parts);
    else this.titular.firstDigits = onlyDigits(value, 6);
  }

  onTitularLast(value: string): void {
    const parts = splitCardNumber(value);
    if (parts) this.applyTitularParts(parts);
    else this.titular.lastFour = onlyDigits(value, 4);
  }

  private applyTitularParts(parts: { firstDigits: string; lastFour: string }): void {
    this.titular.firstDigits = parts.firstDigits;
    this.titular.lastFour = parts.lastFour;
    this.titularNote.set('Número completo dividido: ficam só os 6 primeiros e os 4 últimos. O meio não é guardado.');
  }

  /** Só preenche a bandeira se ela ainda estiver em branco — nunca troca a escolhida. */
  suggestBrandFromName(): void {
    if (this.newCard.brand) return;
    this.newCard.brand = sugerirBandeiraPeloNome(this.newCard.name) ?? undefined;
  }

  confirmDelete(card: CreditCard): void {
    this.confirmationService.confirm({
      header: 'Excluir cartão',
      message: `Deseja realmente excluir '${card.name}'?`,
      acceptLabel: 'Excluir',
      rejectLabel: 'Cancelar',
      accept: () => this.delete(card),
    });
  }

  private delete(card: CreditCard): void {
    this.cardService.delete(card.id).subscribe({
      next: () => this.load(),
      error: () => {
        this.error.set('Não foi possível excluir. Cartões com lançamentos devem ser desativados.');
      },
    });
  }
}
