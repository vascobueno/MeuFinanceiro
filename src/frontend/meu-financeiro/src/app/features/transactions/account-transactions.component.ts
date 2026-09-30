import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonDirective } from 'primeng/button';
import { Select } from 'primeng/select';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { PaginationComponent } from '../../shared/components/pagination.component';
import { StatCardComponent } from '../../shared/components/stat-card.component';
import { TransactionFiltersComponent } from '../../shared/components/transaction-filters.component';
import { TransactionTableComponent } from '../../shared/components/transaction-table.component';
import { AuthService } from '../../core/services/auth.service';
import { TransactionService } from '../../core/services/transaction.service';
import { AccountService } from '../../core/services/account.service';
import { CategoryService } from '../../core/services/category.service';
import { Account } from '../../core/models/account.model';
import { Category } from '../../core/models/category.model';
import { Transaction, TransactionFilter, TransactionOrigin } from '../../core/models/transaction.model';

/**
 * Lançamentos da conta corrente, com o saldo no topo (docs/06).
 *
 * Separada da tela do cartão porque as duas respondem perguntas diferentes: aqui o número que
 * importa é o saldo; lá, o total da fatura do ciclo.
 */
@Component({
  selector: 'mf-account-transactions',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    ButtonDirective,
    Select,
    MfIconComponent,
    PaginationComponent,
    StatCardComponent,
    TransactionFiltersComponent,
    TransactionTableComponent,
  ],
  templateUrl: './account-transactions.component.html',
  styleUrl: './transactions.shared.scss',
})
export class AccountTransactionsComponent implements OnInit {
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly transactions = signal<Transaction[]>([]);
  readonly totalPages = signal(1);
  readonly totalItems = signal(0);

  readonly accounts = signal<Account[]>([]);
  readonly categories = signal<Category[]>([]);

  readonly accountOptions = computed(() => [
    { id: null, name: 'Todas as contas' },
    ...this.accounts(),
  ]);

  /**
   * Campo comum, não signal, porque é ligado por [(ngModel)] como no resto do app — e por isso
   * os derivados abaixo são getters, não computed(): um computed não reagiria à troca.
   */
  selectedAccountId: string | null = null;

  /** Saldo da conta escolhida; com "Todas", a soma das contas. */
  get balance(): number {
    const accounts = this.accounts();
    return this.selectedAccountId
      ? (accounts.find((a) => a.id === this.selectedAccountId)?.currentBalance ?? 0)
      : accounts.reduce((sum, a) => sum + a.currentBalance, 0);
  }

  get balanceHint(): string {
    if (this.selectedAccountId) return 'conta selecionada';
    const total = this.accounts().length;
    return `${total} conta${total === 1 ? '' : 's'}`;
  }

  // Fora do objeto de filtro de propósito: alimentar [sortField]/[sortOrder] do p-table a partir
  // do que o próprio (onSort) escreve fecha um ciclo e vira loop infinito de requisições.
  private sortBy: string | null = null;
  private sortDir: 'asc' | 'desc' = 'desc';

  readonly filter: TransactionFilter = {
    page: 1,
    pageSize: 15,
    origin: TransactionOrigin.Account,
    categoryId: null,
    accountId: null,
    type: null,
    status: null,
  };

  constructor(
    readonly authService: AuthService,
    private readonly transactionService: TransactionService,
    private readonly accountService: AccountService,
    private readonly categoryService: CategoryService,
  ) {}

  ngOnInit(): void {
    this.accountService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => this.accounts.set(r.items));
    this.categoryService.getAll().subscribe((c) => this.categories.set(c));
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.filter.sortBy = this.sortBy ?? 'transactionDate';
    this.filter.sortDir = this.sortDir;
    this.filter.accountId = this.selectedAccountId;

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

  onAccountChange(): void {
    this.applyFilters();
  }

  onPageChange(page: number): void {
    this.filter.page = page;
    this.load();
  }

  onSort(event: { field?: string; order?: number }): void {
    this.sortBy = event.field ?? null;
    this.sortDir = event.order === 1 ? 'asc' : 'desc';
    this.load();
  }

  remove(transaction: Transaction): void {
    this.transactionService.delete(transaction.id).subscribe({
      next: () => {
        this.load();
        // O saldo muda junto — recarregar as contas mantém o KPI honesto.
        this.accountService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => this.accounts.set(r.items));
      },
      error: () => this.error.set('Não foi possível excluir. Lançamentos conciliados são bloqueados (regra 12).'),
    });
  }
}
