import { Component, ElementRef, OnInit, ViewChild, computed, signal } from '@angular/core';
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
import { AttachmentsComponent } from '../../shared/components/attachments.component';

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
    AttachmentsComponent,
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

  /**
   * Origem vinda da ROTA (/lancamentos/conta/novo ou /cartao/novo). Quando definida, o seletor de
   * origem some: a navegação já disse de onde é. Fica null só na rota neutra, usada pelo botão da
   * lista de Parcelamentos, que não tem contexto de origem.
   */
  readonly fixedOrigin = signal<OriginKind | null>(null);

  /** Rótulo do campo de origem, que acompanha a conta ou o cartão. */
  get originLabel(): string {
    return this.originKind === 'card' ? 'Cartão' : 'Conta';
  }

  /**
   * Transferência é entre contas próprias (regra 9) e o backend zera o cartão — oferecê-la na
   * origem cartão seria oferecer algo que a API recusa.
   */
  get availableTypeOptions(): Array<{ label: string; value: TransactionType }> {
    return this.originKind === 'card'
      ? this.typeOptions.filter((o) => o.value !== TransactionType.Transfer)
      : this.typeOptions;
  }

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

  /** Só existe fora do modo recorrência, por isso é opcional. */
  @ViewChild(AttachmentsComponent) anexos?: AttachmentsComponent;

  // Cartões de modo do parcelamento — radiogroup acessível, navegável por seta.
  @ViewChild('modeCardTotal') private readonly modeCardTotal?: ElementRef<HTMLDivElement>;
  @ViewChild('modeCardInstallment') private readonly modeCardInstallment?: ElementRef<HTMLDivElement>;

  readonly editingId = signal<string | null>(null);
  /** Plano, quando o lançamento em edição é uma parcela — o anexo pertence ao plano, não à parcela. */
  readonly editingPlanId = signal<string | null>(null);
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

    const origin = this.route.snapshot.data['origin'] as OriginKind | undefined;
    if (origin) {
      this.fixedOrigin.set(origin);
      this.originKind = origin;
    }

    // ?modo=parcelamento abre já na aba certa — é como o botão da lista de Parcelamentos entra.
    const modo = this.route.snapshot.queryParamMap.get('modo');
    if (modo === 'parcelamento') this.mode = 'installment';
    if (modo === 'recorrencia') this.mode = 'recurring';

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
    this.editingPlanId.set(transaction.installmentPlanId ?? null);
  }

  /**
   * A que o anexo se prende. Editando uma parcela, o dono é o PLANO — a nota da compra vale para
   * todas as parcelas, não só para a que está aberta.
   *
   * Getter, e não computed(): `mode` é campo comum ligado por ngModel, não signal, então um
   * computed não recalcularia ao trocar de modo.
   */
  get attachmentOwnerKind(): 'transaction' | 'installmentPlan' {
    if (this.editingPlanId()) return 'installmentPlan';
    if (!this.editingId() && this.mode === 'installment') return 'installmentPlan';
    return 'transaction';
  }

  /** Null na criação: o id ainda não existe e o componente segura os arquivos até o save. */
  get attachmentOwnerId(): string | null {
    return this.editingPlanId() ?? this.editingId();
  }

  selectInstallmentMode(value: InstallmentMode, focus = false): void {
    this.installmentMode = value;
    if (focus) {
      const target = value === InstallmentMode.TotalAmount ? this.modeCardTotal : this.modeCardInstallment;
      target?.nativeElement.focus();
    }
  }

  /** Seta move entre os dois cartões, como manda o padrão de radiogroup. */
  onModeKeydown(event: KeyboardEvent): void {
    if (!['ArrowRight', 'ArrowDown', 'ArrowLeft', 'ArrowUp'].includes(event.key)) return;
    event.preventDefault();
    const next = this.installmentMode === InstallmentMode.TotalAmount
      ? InstallmentMode.InstallmentAmount
      : InstallmentMode.TotalAmount;
    this.selectInstallmentMode(next, true);
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
      next: (created) => this.uploadAttachmentsThenLeave(created),
      error: () => {
        this.saving.set(false);
        this.error.set('Não foi possível salvar o lançamento. Verifique os dados informados.');
      },
    });
  }

  /**
   * Na criação os anexos ficam segurados no componente até aqui, porque só agora existe a que
   * vinculá-los. Num lançamento parcelado o dono é o PLANO, não cada parcela: a nota da compra é
   * um documento só e vale para as 12 parcelas.
   */
  private uploadAttachmentsThenLeave(created: Transaction[]): void {
    const leave = () => this.router.navigate(['/lancamentos']);

    const anexos = this.anexos;
    if (!anexos?.hasPending() || created.length === 0) {
      leave();
      return;
    }

    const owner =
      this.mode === 'installment'
        ? created[0].installmentPlanId
          ? { kind: 'installmentPlan' as const, id: created[0].installmentPlanId }
          : null
        : { kind: 'transaction' as const, id: created[0].id };

    if (!owner) {
      leave();
      return;
    }

    anexos.uploadPending(owner).subscribe({
      next: () => leave(),
      // O lançamento já foi gravado — perder o anexo não pode desfazê-lo nem prender o usuário
      // na tela; avisa e segue.
      error: () => {
        this.saving.set(false);
        this.error.set('Lançamento salvo, mas não foi possível enviar os anexos. Tente anexá-los na edição.');
      },
    });
  }
}
