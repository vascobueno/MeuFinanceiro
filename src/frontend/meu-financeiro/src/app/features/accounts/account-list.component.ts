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
import { nextSort } from '../../core/services/sort.util';
import { businessRuleOf } from '../../core/services/problem.util';
import { BANCOS, OUTRO_BANCO, bancoPorCodigo } from '../../core/data/bancos';

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
  readonly editingId = signal<string | null>(null);
  readonly newAccount: CreateAccountRequest = AccountListComponent.emptyForm();
  private editingIsActive = true;

  readonly OUTRO_BANCO = OUTRO_BANCO;
  // Os mais comuns primeiro, pelo nome; os demais (lista do Banco Central) pelo código. O filtro
  // do seletor procura em todos, pelo código ou pelo nome.
  readonly bankOptions = [
    ...[...BANCOS].filter((b) => b.popular).sort((a, b) => a.name.localeCompare(b.name, 'pt-BR')),
    ...BANCOS.filter((b) => !b.popular),
  ]
    .map((b) => ({ label: b.code + ' – ' + b.name, value: b.code }))
    .concat({ label: 'Outro', value: OUTRO_BANCO });
  /** Código escolhido no seletor, "outro" ou null (sem dados bancários). */
  bankChoice: string | null = null;

  private static emptyForm(): CreateAccountRequest {
    return {
      name: '', bank: '', type: AccountType.Checking, initialBalance: 0, overdraftLimit: 0,
      bankCode: null, agency: '', agencyDigit: '', accountNumber: '', accountDigit: '',
    };
  }

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
    // A direção vem de nextSort, e não do `order` do evento: a tabela é recriada a cada carga
    // e o p-table emite sempre ascendente (core/services/sort.util.ts).
    const proxima = nextSort({ sortBy: this.sortBy, sortDir: this.sortDir }, event.field, 'name');
    this.sortBy = proxima.sortBy;
    this.sortDir = proxima.sortDir;
    this.load();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  openForm(): void {
    this.editingId.set(null);
    Object.assign(this.newAccount, AccountListComponent.emptyForm());
    this.bankChoice = null;
    this.showForm.set(true);
    this.formError.set(null);
  }

  edit(account: Account): void {
    this.editingId.set(account.id);
    this.editingIsActive = account.isActive;
    Object.assign(this.newAccount, {
      name: account.name,
      bank: account.bank ?? '',
      type: account.type,
      initialBalance: account.initialBalance,
      overdraftLimit: account.overdraftLimit ?? 0,
      bankCode: account.bankCode ?? null,
      agency: account.agency ?? '',
      agencyDigit: account.agencyDigit ?? '',
      accountNumber: account.accountNumber ?? '',
      accountDigit: account.accountDigit ?? '',
    });
    // Banco da lista pelo código; banco digitado antes desta tela (só o nome) aparece como "Outro"
    // para o nome não sumir na edição.
    if (bancoPorCodigo(account.bankCode)) this.bankChoice = account.bankCode!;
    else this.bankChoice = account.bankCode || account.bank ? OUTRO_BANCO : null;
    this.showForm.set(true);
    this.formError.set(null);
  }

  /** Banco da lista preenche código e nome; "Outro" abre os dois para digitar; limpar tira tudo. */
  onBankChoice(): void {
    const banco = bancoPorCodigo(this.bankChoice);
    if (banco) {
      this.newAccount.bankCode = banco.code;
      this.newAccount.bank = banco.name;
    } else if (this.bankChoice === OUTRO_BANCO) {
      if (bancoPorCodigo(this.newAccount.bankCode)) {
        this.newAccount.bankCode = '';
        this.newAccount.bank = '';
      }
    } else {
      Object.assign(this.newAccount, { bankCode: null, agency: '', agencyDigit: '', accountNumber: '', accountDigit: '' });
    }
  }

  /** "033 · 0001-X / 12345-6", ou "—". */
  agencyAndAccount(a: Account): string {
    if (!a.accountNumber) return '—';
    const agencia = a.agencyDigit ? a.agency + '-' + a.agencyDigit : a.agency;
    return a.bankCode + ' · ' + agencia + ' / ' + a.accountNumber + '-' + a.accountDigit;
  }

  closeForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
  }

  submit(): void {
    if (!this.newAccount.name.trim()) {
      this.formError.set('Informe o nome da conta.');
      return;
    }

    this.saving.set(true);
    this.formError.set(null);

    const editingId = this.editingId();
    // Campo vazio vai como null: o backend trata "em branco" e "ausente" igual, e o seletor
    // limpo precisa apagar os dados bancários que a conta tinha.
    const blank = (v: string | null | undefined) => (v && v.trim() ? v.trim() : null);
    const payload = {
      name: this.newAccount.name,
      bank: this.newAccount.bank,
      type: this.newAccount.type,
      initialBalance: this.newAccount.initialBalance,
      overdraftLimit: this.newAccount.overdraftLimit ?? 0,
      bankCode: blank(this.newAccount.bankCode),
      agency: blank(this.newAccount.agency),
      agencyDigit: blank(this.newAccount.agencyDigit),
      accountNumber: blank(this.newAccount.accountNumber),
      accountDigit: blank(this.newAccount.accountDigit),
    };
    const request$ = editingId
      ? this.accountService.update(editingId, { ...payload, isActive: this.editingIsActive })
      : this.accountService.create(payload);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.editingId.set(null);
        Object.assign(this.newAccount, AccountListComponent.emptyForm());
        this.bankChoice = null;
        this.load();
        this.loadSummary();
      },
      error: (err) => {
        this.saving.set(false);
        // Regras do backend (dados pela metade, conta duplicada, dígito inválido) já vêm explicadas.
        this.formError.set(
          businessRuleOf(err)?.message ??
            (editingId ? 'Não foi possível salvar as alterações.' : 'Não foi possível salvar a conta.'),
        );
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
