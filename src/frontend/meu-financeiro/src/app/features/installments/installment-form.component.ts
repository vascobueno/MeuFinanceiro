import { Component, ElementRef, OnInit, ViewChild, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { ButtonDirective } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { DatePicker } from 'primeng/datepicker';
import { ProgressSpinner } from 'primeng/progressspinner';
import { InstallmentPlanService } from '../../core/services/installment-plan.service';
import { AccountService } from '../../core/services/account.service';
import { CardService } from '../../core/services/card.service';
import { CategoryService } from '../../core/services/category.service';
import { Account } from '../../core/models/account.model';
import { CreditCard } from '../../core/models/card.model';
import { Category } from '../../core/models/category.model';
import { InstallmentMode } from '../../core/models/transaction.model';
import { MoneyInputComponent } from '../../shared/components/money-input.component';
import { AttachmentsComponent } from '../../shared/components/attachments.component';

/**
 * Tela Parcelamento, espelhando docs/screens/parcelamento/mockup.html. O mockup só mostra
 * modo/valor/quantidade/primeira parcela; descrição e origem (conta ou cartão) foram
 * adicionadas aqui porque são obrigatórias para criar lançamentos reais (regra 3/21) e o
 * mockup não previa esse contexto.
 */
@Component({
  selector: 'mf-installment-form',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MfIconComponent,
    ButtonDirective,
    InputText,
    Select,
    DatePicker,
    ProgressSpinner,
    MoneyInputComponent,
    AttachmentsComponent,
  ],
  templateUrl: './installment-form.component.html',
  styleUrl: './installment-form.component.scss',
})
export class InstallmentFormComponent implements OnInit {
  readonly InstallmentMode = InstallmentMode;

  @ViewChild('modeCardTotal') private readonly modeCardTotal?: ElementRef<HTMLDivElement>;
  @ViewChild('modeCardInstallment') private readonly modeCardInstallment?: ElementRef<HTMLDivElement>;
  @ViewChild(AttachmentsComponent) private readonly anexos?: AttachmentsComponent;

  readonly accounts = signal<Account[]>([]);
  readonly cards = signal<CreditCard[]>([]);
  readonly categories = signal<Category[]>([]);

  readonly accountOptions = computed(() => [{ id: null, name: 'Nenhuma' }, ...this.accounts()]);
  readonly cardOptions = computed(() => [{ id: null, name: 'Nenhum' }, ...this.cards()]);
  readonly categoryOptions = computed(() => [{ id: null, name: 'Nenhuma' }, ...this.categories()]);

  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);

  description = '';
  accountId: string | null = null;
  creditCardId: string | null = null;
  categoryId: string | null = null;

  mode: InstallmentMode = InstallmentMode.TotalAmount;
  amount: number | null = null;
  installmentCount = 2;
  // Date (não string) por ser ligado a [(ngModel)] de mat-datepicker — ver comentário em
  // transaction-form.component.ts sobre o loop infinito de change detection que uma
  // conversão string->Date refeita a cada leitura provoca sob Angular zoneless.
  firstDueDate: Date = new Date();

  constructor(
    private readonly installmentPlanService: InstallmentPlanService,
    private readonly accountService: AccountService,
    private readonly cardService: CardService,
    private readonly categoryService: CategoryService,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    this.accountService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => this.accounts.set(r.items));
    this.cardService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => this.cards.set(r.items));
    this.categoryService.getAll().subscribe((c) => this.categories.set(c));
  }

  /**
   * Espelha MeuFinanceiro.Application.Installments.InstallmentCalculator (regras 15/16/17) só
   * para a prévia local; o cálculo que efetivamente grava os dados é sempre o do backend.
   * Método simples chamado direto do template (não signal/computed): sob zoneless, cada
   * (ngModelChange) já é um evento de template que dispara verificação, então o método é
   * reavaliado a cada digitação sem precisar de estado reativo próprio.
   */
  preview(): string | null {
    const { mode, amount, installmentCount: count } = this;
    if (amount === null || amount <= 0 || count < 1) return null;

    let installmentAmount: number;
    let total: number;

    if (mode === InstallmentMode.TotalAmount) {
      installmentAmount = Math.trunc((amount / count) * 100) / 100;
      total = amount;
    } else {
      installmentAmount = amount;
      total = amount * count;
    }

    return `${count} parcelas de ${this.formatCurrency(installmentAmount)} — total ${this.formatCurrency(total)}`;
  }

  selectMode(value: InstallmentMode, focus = false): void {
    this.mode = value;
    if (focus) {
      const target = value === InstallmentMode.TotalAmount ? this.modeCardTotal : this.modeCardInstallment;
      target?.nativeElement.focus();
    }
  }

  // Só 2 opções, então qualquer seta (não só ←/→ como um mat-radio-group faria) alterna entre
  // elas — mantém a navegação por teclado equivalente à do radio group que este seletor substitui.
  onModeKeydown(event: KeyboardEvent): void {
    if (!['ArrowRight', 'ArrowDown', 'ArrowLeft', 'ArrowUp'].includes(event.key)) return;
    event.preventDefault();
    const next = this.mode === InstallmentMode.TotalAmount ? InstallmentMode.InstallmentAmount : InstallmentMode.TotalAmount;
    this.selectMode(next, true);
  }

  private formatCurrency(value: number): string {
    return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }

  submit(): void {
    if (!this.description.trim()) {
      this.error.set('Informe a descrição.');
      return;
    }
    if (!this.accountId && !this.creditCardId) {
      this.error.set('Informe a conta ou o cartão de origem.');
      return;
    }
    if (this.amount === null || this.amount <= 0) {
      this.error.set('Informe o valor.');
      return;
    }
    if (this.installmentCount < 1) {
      this.error.set('A quantidade de parcelas deve ser maior que zero.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.success.set(null);

    this.installmentPlanService
      .create({
        description: this.description,
        accountId: this.accountId,
        creditCardId: this.creditCardId,
        categoryId: this.categoryId,
        mode: this.mode,
        amount: this.amount,
        installmentCount: this.installmentCount,
        firstDueDate: this.toIsoDate(this.firstDueDate)!,
      })
      .subscribe({
        next: (result) => {
          const resumo = `${result.installmentCount} parcelas criadas — total ${this.formatCurrency(result.totalAmount)}.`;

          // Os anexos ficaram segurados até aqui: só agora existe o plano a que vinculá-los.
          if (!this.anexos?.hasPending()) {
            this.saving.set(false);
            this.success.set(resumo);
            return;
          }

          this.anexos.uploadPending({ kind: 'installmentPlan', id: result.id }).subscribe({
            next: () => {
              this.saving.set(false);
              this.success.set(`${resumo} Anexos enviados.`);
            },
            // O parcelamento já foi criado — falha de anexo não o desfaz.
            error: () => {
              this.saving.set(false);
              this.success.set(resumo);
              this.error.set('As parcelas foram criadas, mas não foi possível enviar os anexos.');
            },
          });
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(err?.error?.detail ?? 'Não foi possível criar o parcelamento.');
        },
      });
  }

  goToTransactions(): void {
    this.router.navigate(['/lancamentos']);
  }

  private toIsoDate(value: Date | null): string | null {
    if (!value) return null;
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
