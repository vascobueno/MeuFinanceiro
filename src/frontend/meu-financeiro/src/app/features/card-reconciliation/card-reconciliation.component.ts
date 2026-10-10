import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { ButtonDirective } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { DatePicker } from 'primeng/datepicker';
import { Textarea } from 'primeng/textarea';
import { Table, SortIcon, SortableColumn } from 'primeng/table';
import { AuthService } from '../../core/services/auth.service';
import { ReconciliationService } from '../../core/services/reconciliation.service';
import { CardService } from '../../core/services/card.service';
import { CardReconciliationItem, ImportCardInvoiceRequest, ImportLineError } from '../../core/models/reconciliation.model';
import { businessRuleOf } from '../../core/services/problem.util';
import { CreditCard } from '../../core/models/card.model';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { StatCardComponent } from '../../shared/components/stat-card.component';

/** docs/08-conciliacao-cartoes.md — entrada inicial: PDF da fatura (normalizado aqui manualmente). */
@Component({
  selector: 'mf-card-reconciliation',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MfIconComponent,
    ButtonDirective,
    InputText,
    Select,
    DatePicker,
    Textarea,
    Table,
    SortIcon,
    SortableColumn,
    ListStateComponent,
    StatCardComponent,
  ],
  templateUrl: './card-reconciliation.component.html',
  styleUrl: './card-reconciliation.component.scss',
})
export class CardReconciliationComponent implements OnInit {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<CardReconciliationItem[]>([]);
  readonly cards = signal<CreditCard[]>([]);
  readonly cardFilterOptions = computed(() => [{ id: null, name: 'Todos' }, ...this.cards()]);

  // KPIs calculados a partir dos itens já carregados na tela (sem endpoint de agregação novo).
  get matchedCount(): number {
    return this.items().filter((i) => i.status === 'Matched' || i.status === 'ManuallyMatched').length;
  }
  get possibleCount(): number {
    return this.items().filter((i) => i.status === 'PossibleMatch').length;
  }
  get unmatchedCount(): number {
    return this.items().filter((i) => i.status === 'Unmatched').length;
  }

  readonly statusOptions = [
    { label: 'Todos', value: '' },
    { label: 'Conciliado', value: 'Matched' },
    { label: 'Possível match', value: 'PossibleMatch' },
    { label: 'Sem correspondência', value: 'Unmatched' },
  ];

  selectedCardId: string | null = null;
  selectedStatus = '';

  readonly showImportForm = signal(false);
  importCardId: string | null = null;
  // Date (não string) por ser ligado a [(ngModel)] de p-datepicker — ver comentário em
  // transaction-form.component.ts sobre o loop infinito de change detection que uma
  // conversão string->Date refeita a cada leitura provoca sob Angular zoneless.
  closingDate: Date | null = null;
  dueDate: Date | null = null;
  totalAmount: number | null = null;
  /** Saldo da fatura anterior: a fatura fecha como saldo anterior + itens = total. */
  previousBalance: number | null = null;
  /** Uma linha por item: data;descrição;parcela;valor */
  itemsRaw = '';
  readonly importing = signal(false);
  readonly importResult = signal<string | null>(null);
  readonly parseErrors = signal<ImportLineError[]>([]);
  readonly unbalancedMessage = signal<string | null>(null);
  private pendingImport: ImportCardInvoiceRequest | null = null;

  constructor(
    private readonly reconciliationService: ReconciliationService,
    private readonly cardService: CardService,
    readonly authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.cardService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => {
      this.cards.set(r.items);
      if (r.items.length > 0) this.importCardId = r.items[0].id;
    });
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.reconciliationService.getCardReconciliations(this.selectedCardId ?? undefined, this.selectedStatus || undefined).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar as conciliações.');
        this.loading.set(false);
      },
    });
  }

  /** Leitura do texto pelo servidor — ver o comentário equivalente na conciliação bancária. */
  submitImport(): void {
    this.importResult.set(null);
    this.parseErrors.set([]);
    this.unbalancedMessage.set(null);

    const closingDate = this.toIsoDate(this.closingDate);
    const dueDate = this.toIsoDate(this.dueDate);
    if (!this.importCardId || !closingDate || !dueDate || this.totalAmount == null) {
      this.importResult.set('Preencha cartão, fechamento, vencimento e total — o fechamento dá o ano às datas dd/MM da fatura.');
      return;
    }
    if (!this.itemsRaw.trim()) {
      this.importResult.set('Cole os itens da fatura.');
      return;
    }

    const creditCardId = this.importCardId;
    const totalAmount = this.totalAmount;
    this.importing.set(true);
    this.reconciliationService.parseCardInvoice({ text: this.itemsRaw, closingDate }).subscribe({
      next: (parsed) => {
        if (parsed.errors.length > 0) {
          this.importing.set(false);
          this.parseErrors.set(parsed.errors);
          return;
        }
        if (parsed.items.length === 0) {
          this.importing.set(false);
          this.importResult.set('Nenhum item encontrado no texto colado.');
          return;
        }
        this.sendImport({
          creditCardId,
          closingDate,
          dueDate,
          totalAmount,
          previousBalance: this.previousBalance ?? 0,
          items: parsed.items,
        });
      },
      error: () => {
        this.importing.set(false);
        this.importResult.set('Não foi possível ler o texto colado.');
      },
    });
  }

  confirmUnbalanced(): void {
    if (this.pendingImport) this.sendImport({ ...this.pendingImport, confirmUnbalanced: true });
  }

  cancelUnbalanced(): void {
    this.pendingImport = null;
    this.unbalancedMessage.set(null);
  }

  private sendImport(request: ImportCardInvoiceRequest): void {
    this.importing.set(true);
    this.unbalancedMessage.set(null);

    this.reconciliationService.importCardInvoice(request).subscribe({
      next: (invoice) => {
        this.importing.set(false);
        this.pendingImport = null;
        this.importResult.set(
          invoice.totalValidated
            ? `Fatura importada (${invoice.itemCount} itens) e o total confere. ✅`
            : `Fatura importada (${invoice.itemCount} itens) sem fechar com o total, como confirmado. ⚠️`,
        );
        this.itemsRaw = '';
        this.load();
      },
      error: (err) => {
        this.importing.set(false);
        const rule = businessRuleOf(err);
        if (rule?.rule === 'fatura-nao-fecha') {
          this.pendingImport = request;
          this.unbalancedMessage.set(rule.message);
          return;
        }
        this.importResult.set(rule?.message ?? 'Não foi possível importar a fatura.');
      },
    });
  }

  accept(item: CardReconciliationItem): void {
    this.reconciliationService.acceptCardReconciliation(item.id).subscribe({ next: () => this.load() });
  }

  undo(item: CardReconciliationItem): void {
    this.reconciliationService.undoCardReconciliation(item.id).subscribe({ next: () => this.load() });
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'Matched':
        return 'Conciliado';
      case 'PossibleMatch':
        return 'Possível match';
      case 'ManuallyMatched':
        return 'Conciliado manualmente';
      case 'Unmatched':
        return 'Sem correspondência';
      default:
        return status;
    }
  }

  statusClass(status: string): string {
    switch (status) {
      case 'Matched':
      case 'ManuallyMatched':
        return 'mf-badge-ok';
      case 'PossibleMatch':
        return 'mf-badge-warn';
      default:
        return 'mf-badge-neutral';
    }
  }

  private toIsoDate(value: Date | null): string | null {
    if (!value) return null;
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
