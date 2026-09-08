import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { ButtonDirective } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { Table, SortIcon, SortableColumn } from 'primeng/table';
import { Tooltip } from 'primeng/tooltip';
import { SortEvent } from 'primeng/api';
import { AuthService } from '../../core/services/auth.service';
import { AccountService } from '../../core/services/account.service';
import { InvestmentService } from '../../core/services/investment.service';
import { Account, AccountType, CreateAccountRequest } from '../../core/models/account.model';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { PaginationComponent } from '../../shared/components/pagination.component';
import { StatCardComponent } from '../../shared/components/stat-card.component';

/** Tela de contas (RF002), espelhando docs/screens/contas/mockup.html. */
@Component({
  selector: 'mf-account-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MfIconComponent,
    ButtonDirective,
    InputText,
    Select,
    Table,
    SortIcon,
    SortableColumn,
    ListStateComponent,
    PaginationComponent,
    StatCardComponent,
    ConfirmDialog,
    Tooltip,
  ],
  providers: [ConfirmationService],
  templateUrl: './account-list.component.html',
  styleUrl: './account-list.component.scss',
})
export class AccountListComponent implements OnInit {
  readonly AccountType = AccountType;
  readonly typeOptions = [
    { label: 'Corrente', value: AccountType.Checking },
    { label: 'Poupança', value: AccountType.Savings },
  ];

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly accounts = signal<Account[]>([]);
  readonly page = signal(1);
  readonly totalPages = signal(0);
  readonly totalItems = signal(0);
  sortBy?: string;
  sortDir: 'asc' | 'desc' = 'asc';

  // Somas para os KPIs (regra 7/8): busca à parte, sem paginação, independente da tabela.
  readonly accountsTotalBalance = signal(0);
  readonly activeAccountsCount = signal(0);
  readonly investmentsBalance = signal(0);

  readonly showForm = signal(false);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  readonly newAccount: CreateAccountRequest = { name: '', bank: '', type: AccountType.Checking, initialBalance: 0 };

  constructor(
    private readonly accountService: AccountService,
    private readonly investmentService: InvestmentService,
    private readonly confirmationService: ConfirmationService,
    readonly authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.loadSummary();
    this.load();
  }

  private loadSummary(): void {
    this.accountService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => {
      this.accountsTotalBalance.set(r.items.reduce((sum, a) => sum + a.currentBalance, 0));
      this.activeAccountsCount.set(r.items.filter((a) => a.isActive).length);
    });
    this.investmentService.getSummary().subscribe((s) => this.investmentsBalance.set(s.totalPosition));
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.accountService.getAll({ page: this.page(), pageSize: 10, sortBy: this.sortBy, sortDir: this.sortDir }).subscribe({
      next: (result) => {
        this.accounts.set(result.items);
        this.totalPages.set(result.totalPages);
        this.totalItems.set(result.totalItems);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar as contas.');
        this.loading.set(false);
      },
    });
  }

  onSort(event: SortEvent): void {
    this.sortBy = event.field;
    this.sortDir = event.order === -1 ? 'desc' : 'asc';
    this.load();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  openForm(): void {
    this.showForm.set(true);
    this.formError.set(null);
  }

  closeForm(): void {
    this.showForm.set(false);
  }

  submit(): void {
    if (!this.newAccount.name.trim()) {
      this.formError.set('Informe o nome da conta.');
      return;
    }

    this.saving.set(true);
    this.formError.set(null);

    this.accountService.create(this.newAccount).subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.newAccount.name = '';
        this.newAccount.bank = '';
        this.newAccount.initialBalance = 0;
        this.load();
        this.loadSummary();
      },
      error: () => {
        this.saving.set(false);
        this.formError.set('Não foi possível salvar a conta.');
      },
    });
  }

  confirmDelete(account: Account): void {
    this.confirmationService.confirm({
      header: 'Excluir conta',
      message: `Deseja realmente excluir '${account.name}'?`,
      acceptLabel: 'Excluir',
      rejectLabel: 'Cancelar',
      accept: () => this.delete(account),
    });
  }

  private delete(account: Account): void {
    this.accountService.delete(account.id).subscribe({
      next: () => {
        this.load();
        this.loadSummary();
      },
      error: () => {
        this.error.set('Não foi possível excluir. Contas com lançamentos devem ser desativadas.');
      },
    });
  }
}
