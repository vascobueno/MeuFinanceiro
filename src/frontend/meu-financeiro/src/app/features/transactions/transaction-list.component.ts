import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule, MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatSortModule, Sort } from '@angular/material/sort';
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
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog.component';

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
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatTableModule,
    MatSortModule,
    ListStateComponent,
    PaginationComponent,
    StatusBadgeComponent,
    DateRangeComponent,
  ],
  providers: [{ provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { floatLabel: 'always', subscriptSizing: 'dynamic' } }],
  templateUrl: './transaction-list.component.html',
  styleUrl: './transaction-list.component.scss',
})
export class TransactionListComponent implements OnInit {
  readonly TransactionType = TransactionType;
  readonly TransactionStatus = TransactionStatus;
  readonly displayedColumns = ['transactionDate', 'description', 'categoryName', 'origin', 'amount', 'status', 'actions'];

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly transactions = signal<Transaction[]>([]);
  readonly totalPages = signal(0);
  readonly totalItems = signal(0);

  readonly accounts = signal<Account[]>([]);
  readonly cards = signal<CreditCard[]>([]);
  readonly categories = signal<Category[]>([]);

  readonly dateRange: DateRange = { start: null, end: null };

  readonly filter: TransactionFilter = {
    page: 1,
    pageSize: 15,
    sortBy: 'transactionDate',
    sortDir: 'desc',
    search: '',
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
    private readonly dialog: MatDialog,
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

  onSortChange(sort: Sort): void {
    this.filter.sortBy = sort.active;
    this.filter.sortDir = sort.direction === 'desc' ? 'desc' : 'asc';
    this.load();
  }

  confirmDelete(transaction: Transaction): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Excluir lançamento',
          message: `Deseja realmente excluir '${transaction.description}'?`,
          confirmLabel: 'Excluir',
        },
      })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) this.delete(transaction);
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
