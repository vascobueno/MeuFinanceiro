import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonDirective } from 'primeng/button';
import { Select } from 'primeng/select';
import { DatePicker } from 'primeng/datepicker';
import { Tooltip } from 'primeng/tooltip';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { PaginationComponent } from '../../shared/components/pagination.component';
import { StatCardComponent } from '../../shared/components/stat-card.component';
import { MoneyInputComponent } from '../../shared/components/money-input.component';
import { TransactionFiltersComponent } from '../../shared/components/transaction-filters.component';
import { TransactionTableComponent } from '../../shared/components/transaction-table.component';
import { AuthService } from '../../core/services/auth.service';
import { TransactionService } from '../../core/services/transaction.service';
import { CardService } from '../../core/services/card.service';
import { AccountService } from '../../core/services/account.service';
import { CategoryService } from '../../core/services/category.service';
import { CardPaymentService } from '../../core/services/card-payment.service';
import { Account } from '../../core/models/account.model';
import { CreditCard } from '../../core/models/card.model';
import { Category } from '../../core/models/category.model';
import { CardInvoiceCycle, CardPayment } from '../../core/models/card-payment.model';
import { Transaction, TransactionFilter, TransactionOrigin } from '../../core/models/transaction.model';
import { parseIsoDate, shiftMonthIso } from '../../core/services/date.util';

/**
 * Lançamentos do cartão, com a fatura do ciclo no topo (docs/07).
 *
 * A fatura mostrada é calculada a partir dos lançamentos do ciclo — não é a `CardInvoice`
 * importada do banco, que continua na tela Faturas para conciliação.
 *
 * Os pagamentos NÃO aparecem na tabela de lançamentos: eles são lançamentos da conta, não do
 * cartão. Ficam na seção própria acima, e aparecem na tela da conta como "Pagamento de cartão".
 */
@Component({
  selector: 'mf-card-transactions',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    ButtonDirective,
    Select,
    DatePicker,
    Tooltip,
    ConfirmDialog,
    MfIconComponent,
    PaginationComponent,
    StatCardComponent,
    MoneyInputComponent,
    TransactionFiltersComponent,
    TransactionTableComponent,
  ],
  providers: [ConfirmationService],
  templateUrl: './card-transactions.component.html',
  styleUrl: './transactions.shared.scss',
})
export class CardTransactionsComponent implements OnInit {
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly transactions = signal<Transaction[]>([]);
  readonly totalPages = signal(1);
  readonly totalItems = signal(0);

  readonly cards = signal<CreditCard[]>([]);
  readonly accounts = signal<Account[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly cycle = signal<CardInvoiceCycle | null>(null);
  readonly cycleLoading = signal(false);

  /** Campo comum porque é ligado por [(ngModel)]; ver o mesmo padrão na tela da conta. */
  selectedCardId: string | null = null;

  /** Data de fechamento do ciclo exibido; null = ciclo atual. */
  private reference: string | null = null;

  // Formulário inline de pagamento, no padrão de account-list (o app não usa diálogo).
  readonly showPaymentForm = signal(false);
  readonly editingPaymentId = signal<string | null>(null);
  readonly savingPayment = signal(false);
  readonly paymentError = signal<string | null>(null);

  payment = { amount: 0, date: new Date(), accountId: null as string | null };

  private sortBy: string | null = null;
  private sortDir: 'asc' | 'desc' = 'desc';

  readonly filter: TransactionFilter = {
    page: 1,
    pageSize: 15,
    origin: TransactionOrigin.Card,
    categoryId: null,
    creditCardId: null,
    type: null,
    status: null,
  };

  constructor(
    readonly authService: AuthService,
    private readonly transactionService: TransactionService,
    private readonly cardService: CardService,
    private readonly accountService: AccountService,
    private readonly categoryService: CategoryService,
    private readonly cardPaymentService: CardPaymentService,
    private readonly confirmationService: ConfirmationService,
  ) {}

  get accountOptions(): Array<{ id: string | null; name: string }> {
    return [{ id: null, name: 'Outra forma (sem conta)' }, ...this.accounts()];
  }

  /** Rótulo do ciclo: "Fatura de MM/aaaa", pelo mês do fechamento. */
  get cycleLabel(): string {
    const current = this.cycle();
    if (!current) return '';
    // parseIsoDate, e não new Date(iso): este último é UTC e desloca o mês em fuso negativo.
    const mes = parseIsoDate(current.reference);
    return `Fatura de ${String(mes.getMonth() + 1).padStart(2, '0')}/${mes.getFullYear()}`;
  }

  get cycleBadge(): { text: string; tone: 'neutral' | 'bad' } {
    const current = this.cycle();
    if (!current) return { text: '', tone: 'neutral' };
    if (current.isOverdue) return { text: 'Vencida', tone: 'bad' };
    return current.isClosed ? { text: 'Fechada', tone: 'neutral' } : { text: 'Aberta', tone: 'neutral' };
  }

  ngOnInit(): void {
    this.categoryService.getAll().subscribe((c) => this.categories.set(c));
    this.accountService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => this.accounts.set(r.items));

    this.cardService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => {
      this.cards.set(r.items);
      // Sem cartão escolhido não há fatura nem janela de datas — assume o primeiro ativo.
      this.selectedCardId = r.items.find((c) => c.isActive)?.id ?? r.items[0]?.id ?? null;
      if (this.selectedCardId) this.loadCycle();
    });
  }

  // ---------------- ciclo ----------------

  loadCycle(): void {
    if (!this.selectedCardId) return;

    this.cycleLoading.set(true);
    this.cardPaymentService.getCycle(this.selectedCardId, this.reference).subscribe({
      next: (cycle) => {
        this.cycle.set(cycle);
        this.reference = cycle.reference;
        this.cycleLoading.set(false);
        this.loadTransactions();
      },
      error: () => {
        this.error.set('Não foi possível carregar a fatura.');
        this.cycleLoading.set(false);
      },
    });
  }

  onCardChange(): void {
    // Cartões têm fechamentos diferentes: manter a referência do anterior pediria um ciclo que
    // não existe neste cartão, e o backend recusaria com 400.
    this.reference = null;
    this.cycle.set(null);
    this.closePaymentForm();
    this.loadCycle();
  }

  /**
   * Anda um ciclo. Como a referência é o MÊS (dia 1), basta andar o mês — o backend resolve o
   * fechamento real, inclusive o ajustado por fatura.
   *
   * Antes isto replicava aqui o encurtamento de fim de mês do backend, e era onde o bug morava:
   * em cartão que fecha no dia 1, avançar não saía do lugar e voltar pulava dois meses.
   */
  shiftCycle(months: number): void {
    const current = this.cycle();
    if (!current) return;

    this.reference = shiftMonthIso(current.reference, months);
    this.loadCycle();
  }

  // ---------------- lançamentos ----------------

  loadTransactions(): void {
    const cycle = this.cycle();
    if (!cycle) return;

    this.loading.set(true);
    this.error.set(null);

    this.filter.sortBy = this.sortBy ?? 'transactionDate';
    this.filter.sortDir = this.sortDir;
    this.filter.creditCardId = this.selectedCardId;
    this.filter.startDate = cycle.start;
    this.filter.endDate = cycle.closingDate;

    this.transactionService.getAll(this.filter).subscribe({
      next: (result) => {
        this.transactions.set(result.items);
        this.totalPages.set(result.totalPages);
        this.totalItems.set(result.totalItems);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar os lançamentos.');
        this.loading.set(false);
      },
    });
  }

  applyFilters(): void {
    this.filter.page = 1;
    this.loadTransactions();
  }

  onPageChange(page: number): void {
    this.filter.page = page;
    this.loadTransactions();
  }

  onSort(event: { field?: string; order?: number }): void {
    this.sortBy = event.field ?? null;
    this.sortDir = event.order === 1 ? 'asc' : 'desc';
    this.loadTransactions();
  }

  removeTransaction(transaction: Transaction): void {
    this.transactionService.delete(transaction.id).subscribe({
      next: () => this.loadCycle(),
      error: () => this.error.set('Não foi possível excluir. Lançamentos conciliados são bloqueados (regra 12).'),
    });
  }

  // ---------------- pagamento ----------------

  openPaymentForm(): void {
    const cycle = this.cycle();
    this.editingPaymentId.set(null);
    this.payment = {
      // Sugere o que falta pagar: é o valor que o usuário quer em quase todos os casos.
      amount: cycle ? Math.max(cycle.outstanding, 0) : 0,
      date: new Date(),
      accountId: null,
    };
    this.paymentError.set(null);
    this.showPaymentForm.set(true);
  }

  openPaymentEdit(payment: CardPayment): void {
    this.editingPaymentId.set(payment.id);
    this.payment = {
      amount: payment.amount,
      date: new Date(payment.paymentDate),
      accountId: payment.accountId ?? null,
    };
    this.paymentError.set(null);
    this.showPaymentForm.set(true);
  }

  closePaymentForm(): void {
    this.showPaymentForm.set(false);
    this.editingPaymentId.set(null);
    this.paymentError.set(null);
  }

  submitPayment(): void {
    const cycle = this.cycle();
    if (!cycle || !this.selectedCardId) return;

    if (!this.payment.amount || this.payment.amount <= 0) {
      this.paymentError.set('Informe um valor maior que zero.');
      return;
    }

    this.savingPayment.set(true);
    this.paymentError.set(null);

    const paymentDate = this.toIsoDate(this.payment.date);
    const editingId = this.editingPaymentId();

    const request$ = editingId
      ? this.cardPaymentService.update(editingId, {
          paymentDate,
          amount: this.payment.amount,
          accountId: this.payment.accountId,
        })
      : this.cardPaymentService.create({
          creditCardId: this.selectedCardId,
          cycleReference: cycle.reference,
          paymentDate,
          amount: this.payment.amount,
          accountId: this.payment.accountId,
        });

    request$.subscribe({
      next: () => {
        this.savingPayment.set(false);
        this.closePaymentForm();
        this.loadCycle();
      },
      error: (err) => {
        this.savingPayment.set(false);
        this.paymentError.set(err?.error?.detail ?? 'Não foi possível salvar o pagamento.');
      },
    });
  }

  confirmRemovePayment(payment: CardPayment): void {
    this.confirmationService.confirm({
      header: 'Excluir pagamento',
      message: payment.accountId
        ? 'Excluir este pagamento? O lançamento na conta também será removido.'
        : 'Excluir este pagamento?',
      acceptLabel: 'Excluir',
      rejectLabel: 'Cancelar',
      accept: () =>
        this.cardPaymentService.delete(payment.id).subscribe({
          next: () => this.loadCycle(),
          error: () => this.error.set('Não foi possível excluir o pagamento.'),
        }),
    });
  }

  private toIsoDate(value: Date): string {
    return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
  }
}
