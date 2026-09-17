import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ConfirmationService, SortEvent } from 'primeng/api';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { ButtonDirective } from 'primeng/button';
import { Select } from 'primeng/select';
import { Table, SortIcon, SortableColumn } from 'primeng/table';
import { Tooltip } from 'primeng/tooltip';
import { AuthService } from '../../core/services/auth.service';
import { TransactionService } from '../../core/services/transaction.service';
import { AccountService } from '../../core/services/account.service';
import { CardService } from '../../core/services/card.service';
import { CategoryService } from '../../core/services/category.service';
import { Transaction, TransactionFilter, TransactionStatus, TransactionType } from '../../core/models/transaction.model';
import { Account } from '../../core/models/account.model';
import { CreditCard } from '../../core/models/card.model';
import { Category } from '../../core/models/category.model';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { PaginationComponent } from '../../shared/components/pagination.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge.component';
import { DateRange, DateRangeComponent } from '../../shared/components/date-range.component';

/**
 * Grid de lançamentos (RF004/RF013), espelhando docs/screens/lancamentos/mockup.html.
 * Filtros, ordenação e paginação server-side (regra 19).
 */
@Component({
  selector: 'mf-transaction-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MfIconComponent,
    ButtonDirective,
    Select,
    Table,
    SortIcon,
    SortableColumn,
    ListStateComponent,
    PaginationComponent,
    StatusBadgeComponent,
    DateRangeComponent,
    ConfirmDialog,
    Tooltip,
  ],
  providers: [ConfirmationService],
  templateUrl: './transaction-list.component.html',
  styleUrl: './transaction-list.component.scss',
})
export class TransactionListComponent implements OnInit {
  readonly TransactionType = TransactionType;
  readonly TransactionStatus = TransactionStatus;

  readonly typeOptions = [
    { label: 'Todos', value: null },
    { label: 'Receita', value: TransactionType.Income },
    { label: 'Despesa', value: TransactionType.Expense },
    { label: 'Transferência', value: TransactionType.Transfer },
    { label: 'Aplicação', value: TransactionType.InvestmentApplication },
    { label: 'Resgate', value: TransactionType.InvestmentRedemption },
  ];

  readonly statusOptions = [
    { label: 'Todos', value: null },
    { label: 'Pendente', value: TransactionStatus.Pending },
    { label: 'Confirmado', value: TransactionStatus.Confirmed },
    { label: 'Conciliado', value: TransactionStatus.Reconciled },
  ];

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly transactions = signal<Transaction[]>([]);
  readonly totalPages = signal(0);
  readonly totalItems = signal(0);

  readonly accounts = signal<Account[]>([]);
  readonly cards = signal<CreditCard[]>([]);
  readonly categories = signal<Category[]>([]);

  readonly categoryOptions = computed(() => [{ id: null, name: 'Todas' }, ...this.categories()]);
  readonly accountOptions = computed(() => [{ id: null, name: 'Todas' }, ...this.accounts()]);
  readonly cardOptions = computed(() => [{ id: null, name: 'Todos' }, ...this.cards()]);

  readonly dateRange: DateRange = { start: null, end: null };
  readonly filtersExpanded = signal(false);

  // sortBy/sortDir ficam fora de `filter` de propósito (mesmo padrão em account-list/
  // card-list/invoice-list) — ver comentário sobre [sortField]/[sortOrder] no template: NÃO
  // vinculamos esses inputs do p-table a nada aqui, só usamos (onSort) pra ler o clique do
  // usuário. Ordenação inicial (mais recente primeiro) vem do backend via `load()`, só não
  // fica refletida no ícone do cabeçalho até o primeiro clique — troca aceitável pra evitar
  // o loop infinito documentado no template.
  sortBy?: string;
  sortDir: 'asc' | 'desc' = 'desc';

  readonly filter: TransactionFilter = {
    page: 1,
    pageSize: 15,
    categoryId: null,
    accountId: null,
    creditCardId: null,
    type: null,
    status: null,
  };

  constructor(
    private readonly transactionService: TransactionService,
    private readonly accountService: AccountService,
    private readonly cardService: CardService,
    private readonly categoryService: CategoryService,
    private readonly confirmationService: ConfirmationService,
    readonly authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.accountService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => this.accounts.set(r.items));
    this.cardService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => this.cards.set(r.items));
    this.categoryService.getAll().subscribe((c) => this.categories.set(c));
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.filter.sortBy = this.sortBy ?? 'transactionDate';
    this.filter.sortDir = this.sortDir;
    this.filter.startDate = this.dateRange.start;
    this.filter.endDate = this.dateRange.end;

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
    this.load();
  }

  onDateRangeChange(range: DateRange): void {
    this.dateRange.start = range.start;
    this.dateRange.end = range.end;
    this.applyFilters();
  }

  onPageChange(page: number): void {
    this.filter.page = page;
    this.load();
  }

  onSort(event: SortEvent): void {
    this.sortBy = event.field;
    this.sortDir = event.order === -1 ? 'desc' : 'asc';
    this.load();
  }

  confirmDelete(transaction: Transaction): void {
    this.confirmationService.confirm({
      header: 'Excluir lançamento',
      message: `Deseja realmente excluir '${transaction.description}'?`,
      acceptLabel: 'Excluir',
      rejectLabel: 'Cancelar',
      accept: () => this.delete(transaction),
    });
  }

  private delete(transaction: Transaction): void {
    this.transactionService.delete(transaction.id).subscribe({
      next: () => this.load(),
      error: () => {
        this.error.set('Lançamentos já conciliados não podem ser excluídos automaticamente (regra 12).');
      },
    });
  }
}
