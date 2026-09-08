import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { ButtonDirective } from 'primeng/button';
import { SelectButton } from 'primeng/selectbutton';
import { Select } from 'primeng/select';
import { DatePicker } from 'primeng/datepicker';
import { InputText } from 'primeng/inputtext';
import { Textarea } from 'primeng/textarea';
import { ProgressSpinner } from 'primeng/progressspinner';
import { TransactionService } from '../../core/services/transaction.service';
import { AccountService } from '../../core/services/account.service';
import { CardService } from '../../core/services/card.service';
import { CategoryService } from '../../core/services/category.service';
import {
  CreateTransactionRequest,
  InstallmentMode,
  RecurrenceFrequency,
  Transaction,
  TransactionDirection,
  TransactionStatus,
  TransactionType,
} from '../../core/models/transaction.model';
import { Account } from '../../core/models/account.model';
import { CreditCard } from '../../core/models/card.model';
import { Category } from '../../core/models/category.model';
import { MerchantSuggestion } from '../../core/models/merchant.model';
import { AutocompleteComponent } from '../../shared/components/autocomplete.component';
import { MoneyInputComponent } from '../../shared/components/money-input.component';

type EntryMode = 'simple' | 'installment' | 'recurring';
type OriginKind = 'account' | 'card';

/**
 * Formulário de novo lançamento (RF004/RF006/RF008), espelhando docs/screens/novo-lancamento/mockup.html,
 * docs/screens/recorrencia/mockup.html e docs/screens/parcelamento/mockup.html. Em modo edição (rota /lancamentos/:id) permite apenas
 * ajustes de um lançamento já existente (regra 12: lançamentos conciliados são bloqueados pela API).
 */
@Component({
  selector: 'mf-transaction-form',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MfIconComponent,
    ButtonDirective,
    SelectButton,
    Select,
    DatePicker,
    InputText,
    Textarea,
    ProgressSpinner,
    AutocompleteComponent,
    MoneyInputComponent,
  ],
  templateUrl: './transaction-form.component.html',
  styleUrl: './transaction-form.component.scss',
})
export class TransactionFormComponent implements OnInit {
  readonly TransactionType = TransactionType;
  readonly InstallmentMode = InstallmentMode;
  readonly RecurrenceFrequency = RecurrenceFrequency;

  readonly modeOptions = [
    { label: 'Simples', value: 'simple' },
    { label: 'Parcelamento', value: 'installment' },
    { label: 'Recorrência', value: 'recurring' },
  ];

  readonly typeOptions = [
    { label: 'Despesa', value: TransactionType.Expense },
    { label: 'Receita', value: TransactionType.Income },
    { label: 'Transferência entre contas', value: TransactionType.Transfer },
    { label: 'Aplicação (investimento)', value: TransactionType.InvestmentApplication },
    { label: 'Resgate (investimento)', value: TransactionType.InvestmentRedemption },
  ];

  readonly originKindOptions = [
    { label: 'Conta', value: 'account' },
    { label: 'Cartão', value: 'card' },
  ];

  readonly statusOptions = [
    { label: 'Pendente', value: TransactionStatus.Pending },
    { label: 'Confirmado', value: TransactionStatus.Confirmed },
    { label: 'Conciliado', value: TransactionStatus.Reconciled },
  ];

  readonly installmentModeOptions = [
    { label: 'Valor total ÷ parcelas', value: InstallmentMode.TotalAmount },
    { label: 'Valor por parcela × quantidade', value: InstallmentMode.InstallmentAmount },
  ];

  readonly recurrenceFrequencyOptions = [
    { label: 'Semanal', value: RecurrenceFrequency.Weekly },
    { label: 'Mensal', value: RecurrenceFrequency.Monthly },
    { label: 'Anual', value: RecurrenceFrequency.Yearly },
  ];

  readonly editingId = signal<string | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);

  readonly accounts = signal<Account[]>([]);
  readonly cards = signal<CreditCard[]>([]);
  readonly categories = signal<Category[]>([]);

  readonly accountSelectOptions = computed(() => [{ id: null, name: 'Selecione...' }, ...this.accounts()]);
  readonly cardSelectOptions = computed(() => [{ id: null, name: 'Selecione...' }, ...this.cards()]);
  readonly categorySelectOptions = computed(() => [{ id: null, name: 'Sem categoria' }, ...this.categories()]);

  mode: EntryMode = 'simple';
  originKind: OriginKind = 'account';

  description = '';
  // Guardado como Date (não string) porque é ligado a [(ngModel)] de p-datepicker: um getter
  // que devolvesse um `new Date(...)` novo a cada leitura quebraria a igualdade referencial que
  // o datepicker usa para saber se o valor mudou, disparando escrita -> markForCheck -> nova
  // leitura -> loop infinito de change detection (travou a aba sob Angular zoneless até corrigir).
  // Conversão para string ISO só acontece ao montar o payload (submit()) ou carregar (applyTransactionToForm()).
  transactionDate: Date = new Date();
  type: TransactionType = TransactionType.Expense;
  categoryId: string | null = null;
  accountId: string | null = null;
  creditCardId: string | null = null;
  destinationAccountId: string | null = null;
  amount: number | null = null;
  notes = '';
  status: TransactionStatus = TransactionStatus.Pending;

  installmentMode: InstallmentMode = InstallmentMode.TotalAmount;
  installmentCount = 2;
  firstDueDate: Date = new Date();

  recurrenceFrequency: RecurrenceFrequency = RecurrenceFrequency.Monthly;
  recurrenceEndDate: Date | null = null;

  // new Date('yyyy-MM-dd') interpreta a string como UTC (spec ES2015), então em fusos
  // negativos (ex.: America/Sao_Paulo) o dia local vira o dia anterior — por isso o parse
  // manual dos componentes, que sempre constrói a data em horário local.
  private fromIsoDate(value: string | null | undefined): Date | null {
    if (!value) return null;
    const [year, month, day] = value.split('-').map(Number);
    return new Date(year, month - 1, day);
  }

  private toIsoDate(value: Date | null): string | null {
    if (!value) return null;
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  constructor(
    private readonly transactionService: TransactionService,
    private readonly accountService: AccountService,
    private readonly cardService: CardService,
    private readonly categoryService: CategoryService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    this.accountService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => this.accounts.set(r.items));
    this.cardService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => this.cards.set(r.items));
    this.categoryService.getAll().subscribe((c) => this.categories.set(c));

    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'novo') {
      this.editingId.set(id);
      this.loadForEdit(id);
    }
  }

  private loadForEdit(id: string): void {
    this.loading.set(true);
    this.transactionService.getById(id).subscribe({
      next: (transaction) => {
        this.applyTransactionToForm(transaction);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar o lançamento.');
        this.loading.set(false);
      },
    });
  }

  private applyTransactionToForm(transaction: Transaction): void {
    this.description = transaction.description;
    this.transactionDate = this.fromIsoDate(transaction.transactionDate.substring(0, 10)) ?? new Date();
    this.type = transaction.type;
    this.categoryId = transaction.categoryId ?? null;
    this.accountId = transaction.accountId ?? null;
    this.creditCardId = transaction.creditCardId ?? null;
    this.originKind = transaction.creditCardId ? 'card' : 'account';
    this.amount = transaction.amount;
    this.notes = transaction.notes ?? '';
    this.status = transaction.status;
  }

  onSuggestionSelected(suggestion: MerchantSuggestion): void {
    if (suggestion.defaultCategoryId) this.categoryId = suggestion.defaultCategoryId;
  }

  submit(): void {
    if (!this.description.trim() || !this.amount) {
      this.error.set('Preencha descrição e valor.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.success.set(null);

    if (this.editingId()) {
      this.transactionService
        .update(this.editingId()!, {
          transactionDate: this.toIsoDate(this.transactionDate)!,
          description: this.description,
          categoryId: this.categoryId,
          accountId: this.originKind === 'account' ? this.accountId : null,
          creditCardId: this.originKind === 'card' ? this.creditCardId : null,
          amount: this.amount,
          direction: this.type === TransactionType.Income ? TransactionDirection.Credit : TransactionDirection.Debit,
          status: this.status,
          notes: this.notes,
        })
        .subscribe({
          next: () => this.router.navigate(['/lancamentos']),
          error: () => {
            this.saving.set(false);
            this.error.set('Não foi possível salvar. Lançamentos conciliados não podem ser alterados (regra 12).');
          },
        });
      return;
    }

    const request: CreateTransactionRequest = {
      transactionDate: this.toIsoDate(this.transactionDate)!,
      description: this.description,
      categoryId: this.categoryId,
      accountId: this.type === TransactionType.Transfer || this.originKind === 'account' ? this.accountId : null,
      creditCardId: this.type !== TransactionType.Transfer && this.originKind === 'card' ? this.creditCardId : null,
      destinationAccountId: this.type === TransactionType.Transfer ? this.destinationAccountId : null,
      type: this.type,
      direction: this.type === TransactionType.Income ? TransactionDirection.Credit : TransactionDirection.Debit,
      amount: this.amount,
      notes: this.notes,
      installment:
        this.mode === 'installment'
          ? {
              mode: this.installmentMode,
              amount: this.amount,
              installmentCount: this.installmentCount,
              firstDueDate: this.toIsoDate(this.firstDueDate)!,
            }
          : null,
      recurring:
        this.mode === 'recurring'
          ? { frequency: this.recurrenceFrequency, endDate: this.toIsoDate(this.recurrenceEndDate) }
          : null,
    };

    this.transactionService.create(request).subscribe({
      next: () => this.router.navigate(['/lancamentos']),
      error: () => {
        this.saving.set(false);
        this.error.set('Não foi possível salvar o lançamento. Verifique os dados informados.');
      },
    });
  }
}
