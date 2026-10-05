import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonDirective } from 'primeng/button';
import { Tooltip } from 'primeng/tooltip';
import { Tab, TabList, Tabs } from 'primeng/tabs';
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
import { defaultId, validId } from '../../core/services/selection.util';
import { nextSort } from '../../core/services/sort.util';

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
    Tooltip,
    Tabs,
    TabList,
    Tab,
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

  /** Soma de todas as contas — o número da caixa fora das abas. Pode ser computed: lê signal. */
  readonly totalBalance = computed(() => this.accounts().reduce((sum, a) => sum + a.currentBalance, 0));

  /**
   * Aba ativa. Campo comum, não signal, e por isso os derivados abaixo são getters: um computed
   * não reagiria à troca de aba.
   */
  selectedAccountId: string | null = null;

  get activeAccount(): Account | null {
    return this.accounts().find((a) => a.id === this.selectedAccountId) ?? null;
  }

  /** Saldo só da conta da aba — a linha logo abaixo da faixa. */
  get balance(): number {
    return this.activeAccount?.currentBalance ?? 0;
  }

  get totalHint(): string {
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
    private readonly router: Router,
    private readonly route: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    this.categoryService.getAll().subscribe((c) => this.categories.set(c));

    // loading já aqui: a tabela espera a conta chegar, e sem isto piscaria "nenhum lançamento".
    this.loading.set(true);
    this.accountService.getAll({ page: 1, pageSize: 100 }).subscribe({
      next: (r) => {
        this.accounts.set(r.items);
        // A aba vem da URL; id inválido NÃO vira outra conta aqui, cai no padrão (docs/06).
        this.selectedAccountId =
          validId(r.items, this.route.snapshot.queryParamMap.get('conta')) ?? defaultId(r.items);
        if (this.selectedAccountId) this.load();
        else this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar as contas.');
        this.loading.set(false);
      },
    });
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

  onSelectAccount(id: string | number | undefined): void {
    const accountId = String(id);
    if (!accountId || accountId === this.selectedAccountId) return;

    this.selectedAccountId = accountId;
    // replaceUrl: trocar de aba não é passo de histórico — senão o Voltar do navegador percorre
    // conta por conta antes de sair da tela.
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { conta: accountId },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
    this.applyFilters();
  }

  onPageChange(page: number): void {
    this.filter.page = page;
    this.load();
  }

  onSort(event: { field?: string; order?: number }): void {
    // A direção vem de nextSort, e não do `order` do evento: a tabela é recriada a cada carga
    // e o p-table emite sempre ascendente (core/services/sort.util.ts).
    const proxima = nextSort({ sortBy: this.sortBy, sortDir: this.sortDir }, event.field, 'transactionDate');
    this.sortBy = proxima.sortBy;
    this.sortDir = proxima.sortDir;
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
