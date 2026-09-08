import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { forkJoin } from 'rxjs';
import { AccountService } from '../../core/services/account.service';
import { InvestmentService } from '../../core/services/investment.service';
import { ProjectionService } from '../../core/services/projection.service';
import { TransactionService } from '../../core/services/transaction.service';
import { TransactionDirection, TransactionType } from '../../core/models/transaction.model';
import { StatCardComponent } from '../../shared/components/stat-card.component';
import { TimelineComponent, TimelineEvent } from '../../shared/components/timeline.component';
import { ListStateComponent } from '../../shared/components/list-state.component';

interface CategorySummary {
  name: string;
  amount: number;
  percentOfTop: number;
}

/**
 * Dashboard V4 (docs/screens/dashboard/mockup.html): saldo em contas, investimentos e
 * patrimônio consolidado (regra 8: contas + investimentos só para apresentação, nunca
 * somados de fato — regra 7), compromissos e próximos movimentos (docs/11-projecao-financeira.md)
 * e resumo de despesas do mês corrente por categoria.
 */
@Component({
  selector: 'mf-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, MfIconComponent, StatCardComponent, TimelineComponent, ListStateComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly accountsBalance = signal(0);
  readonly investmentsBalance = signal(0);
  readonly commitments30d = signal(0);
  readonly commitmentsCount = signal(0);
  readonly upcomingEvents = signal<TimelineEvent[]>([]);
  readonly expenseSummary = signal<CategorySummary[]>([]);

  // Comparação simples e real (não decorativa) do saldo em contas hoje / +30 / +60 dias,
  // usada no lugar do gráfico ilustrativo do mockup (que não representa dados reais).
  readonly cashEvolution = signal<{ label: string; value: number }[]>([]);

  get netWorth(): number {
    return this.accountsBalance() + this.investmentsBalance();
  }

  maxCashPoint(): number {
    return Math.max(1, ...this.cashEvolution().map((p) => p.value));
  }

  constructor(
    private readonly accountService: AccountService,
    private readonly investmentService: InvestmentService,
    private readonly projectionService: ProjectionService,
    private readonly transactionService: TransactionService,
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    const today = new Date();
    const in30Days = this.addDays(today, 30);
    const in60Days = this.addDays(today, 60);
    const monthStart = new Date(today.getFullYear(), today.getMonth(), 1);

    forkJoin({
      accounts: this.accountService.getAll({ page: 1, pageSize: 100 }),
      investments: this.investmentService.getSummary(),
      projection30: this.projectionService.getProjection(this.toIsoDate(in30Days)),
      projection60: this.projectionService.getProjection(this.toIsoDate(in60Days)),
      expenses: this.transactionService.getAll({
        page: 1,
        pageSize: 100,
        startDate: this.toIsoDate(monthStart),
        endDate: this.toIsoDate(today),
        type: TransactionType.Expense,
      }),
    }).subscribe({
      next: ({ accounts, investments, projection30, projection60, expenses }) => {
        const accountsTotal = accounts.items.reduce((sum, a) => sum + a.currentBalance, 0);
        this.accountsBalance.set(accountsTotal);
        this.investmentsBalance.set(investments.totalPosition);

        this.cashEvolution.set([
          { label: 'Hoje', value: projection30.currentAccountsBalance },
          { label: '+30 dias', value: projection30.projectedAccountsBalance },
          { label: '+60 dias', value: projection60.projectedAccountsBalance },
        ]);

        const upcoming = projection30.timeline.slice(0, 5);
        this.upcomingEvents.set(
          upcoming.map((entry) => ({
            dateLabel: new Date(entry.date).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' }).toUpperCase(),
            title: entry.description,
            amountLabel: `${entry.amount >= 0 ? '+' : '−'} ${this.formatCurrency(Math.abs(entry.amount))}`,
            positive: entry.amount >= 0,
          })),
        );

        const expenseEntries = projection30.timeline.filter((e) => e.amount < 0);
        this.commitments30d.set(Math.abs(expenseEntries.reduce((sum, e) => sum + e.amount, 0)));
        this.commitmentsCount.set(projection30.timeline.length);

        const totals = new Map<string, number>();
        for (const t of expenses.items) {
          if (t.direction !== TransactionDirection.Debit) continue;
          const key = t.categoryName ?? 'Sem categoria';
          totals.set(key, (totals.get(key) ?? 0) + t.amount);
        }
        const top = [...totals.entries()].sort((a, b) => b[1] - a[1]).slice(0, 3);
        const max = top[0]?.[1] ?? 1;
        this.expenseSummary.set(top.map(([name, amount]) => ({ name, amount, percentOfTop: (amount / max) * 100 })));

        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar o dashboard.');
        this.loading.set(false);
      },
    });
  }

  formatCurrency(value: number): string {
    return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }

  private addDays(date: Date, days: number): Date {
    const result = new Date(date);
    result.setDate(result.getDate() + days);
    return result;
  }

  private toIsoDate(date: Date): string {
    return date.toISOString().substring(0, 10);
  }
}
