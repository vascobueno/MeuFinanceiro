import { Component, OnInit, ViewChild, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule, MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { AuthService } from '../../core/services/auth.service';
import { InvestmentService } from '../../core/services/investment.service';
import { AccountService } from '../../core/services/account.service';
import {
  CreateInvestmentAccountRequest,
  CreateInvestmentRequest,
  Investment,
  InvestmentsSummary,
  InvestmentTransactionType,
} from '../../core/models/investment.model';
import { Account } from '../../core/models/account.model';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { StatCardComponent } from '../../shared/components/stat-card.component';

/**
 * docs/10-investimentos.md: saldo separado das contas correntes (regra 6/7); aplicação
 * reduz conta e aumenta investimento; resgate o inverso; rendimento é evento separado.
 */
@Component({
  selector: 'mf-investments',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatIconModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatTableModule,
    MatSortModule,
    ListStateComponent,
    StatCardComponent,
  ],
  providers: [
    provideNativeDateAdapter(),
    { provide: MAT_DATE_LOCALE, useValue: 'pt-BR' },
    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { floatLabel: 'always', subscriptSizing: 'dynamic' } },
  ],
  templateUrl: './investments.component.html',
  styleUrl: './investments.component.scss',
})
export class InvestmentsComponent implements OnInit {
  readonly InvestmentTransactionType = InvestmentTransactionType;

  // KPIs (docs/screens/investimentos): a partir dos investimentos já carregados na tela.
  get totalPrincipal(): number {
    return this.investments().reduce((sum, i) => sum + i.principal, 0);
  }
  get totalYield(): number {
    return this.investments().reduce((sum, i) => sum + i.netValue, 0) - this.totalPrincipal;
  }

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly summary = signal<InvestmentsSummary | null>(null);
  readonly investments = signal<Investment[]>([]);
  readonly dataSource = new MatTableDataSource<Investment>([]);
  readonly accounts = signal<Account[]>([]);

  // Setter (não ngAfterViewInit) — a <table> só existe depois que loading() vira false, então
  // um @ViewChild lido em ngAfterViewInit pegaria undefined; o setter dispara de novo quando ela aparece.
  @ViewChild(MatSort) set sort(sort: MatSort) {
    if (sort) this.dataSource.sort = sort;
  }

  readonly showAccountForm = signal(false);
  newAccount: CreateInvestmentAccountRequest = { institution: '', name: '' };

  readonly showInvestmentForm = signal(false);
  newInvestment: CreateInvestmentRequest = { investmentAccountId: '', name: '', type: 'CDB' };
  // Date (não string) por ser ligado a [(ngModel)] de mat-datepicker — ver comentário em
  // transaction-form.component.ts sobre o loop infinito de change detection que uma
  // conversão string->Date refeita a cada leitura provoca sob Angular zoneless.
  newInvestmentMaturityDate: Date | null = null;

  readonly txInvestmentId = signal<string | null>(null);
  txType: InvestmentTransactionType = InvestmentTransactionType.Application;
  txAmount: number | null = null;
  txDate: Date = new Date();
  txAccountId: string | null = null;

  constructor(
    private readonly investmentService: InvestmentService,
    private readonly accountService: AccountService,
    readonly authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.accountService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => this.accounts.set(r.items));
    this.load();
  }


  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.investmentService.getSummary().subscribe({
      next: (summary) => {
        this.summary.set(summary);
        if (summary.accounts.length > 0 && !this.newInvestment.investmentAccountId) {
          this.newInvestment.investmentAccountId = summary.accounts[0].id;
        }
      },
      error: () => this.error.set('Não foi possível carregar o resumo de investimentos.'),
    });

    this.investmentService.getInvestments().subscribe({
      next: (investments) => {
        this.investments.set(investments);
        this.dataSource.data = investments;
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar os investimentos.');
        this.loading.set(false);
      },
    });
  }

  createAccount(): void {
    if (!this.newAccount.institution.trim()) return;

    this.investmentService.createAccount(this.newAccount).subscribe({
      next: () => {
        this.newAccount = { institution: '', name: '' };
        this.showAccountForm.set(false);
        this.load();
      },
    });
  }

  createInvestment(): void {
    if (!this.newInvestment.name.trim() || !this.newInvestment.investmentAccountId) return;

    this.investmentService
      .createInvestment({ ...this.newInvestment, maturityDate: this.toIsoDate(this.newInvestmentMaturityDate) })
      .subscribe({
        next: () => {
          this.newInvestment = { investmentAccountId: this.newInvestment.investmentAccountId, name: '', type: 'CDB' };
          this.newInvestmentMaturityDate = null;
          this.showInvestmentForm.set(false);
          this.load();
        },
      });
  }

  openTransactionForm(investment: Investment): void {
    this.txInvestmentId.set(investment.id);
    this.txAmount = null;
  }

  closeTransactionForm(): void {
    this.txInvestmentId.set(null);
  }

  submitTransaction(): void {
    const investmentId = this.txInvestmentId();
    if (!investmentId || !this.txAmount) return;

    this.investmentService
      .registerTransaction({
        investmentId,
        type: this.txType,
        date: this.toIsoDate(this.txDate)!,
        amount: this.txAmount,
        accountId: this.txType === InvestmentTransactionType.Yield ? null : this.txAccountId,
      })
      .subscribe({
        next: () => {
          this.txInvestmentId.set(null);
          this.load();
        },
        error: () => this.error.set('Não foi possível registrar a movimentação.'),
      });
  }

  private toIsoDate(value: Date | null): string | null {
    if (!value) return null;
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
