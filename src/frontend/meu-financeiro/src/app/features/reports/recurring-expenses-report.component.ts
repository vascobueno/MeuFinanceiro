import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonDirective } from 'primeng/button';
import { RecurringService } from '../../core/services/recurring.service';
import { RecurringTransaction } from '../../core/models/recurring.model';
import { RecurrenceFrequency, TransactionType } from '../../core/models/transaction.model';
import { ListStateComponent } from '../../shared/components/list-state.component';

/** Reaproveita o mesmo endpoint da tela Recorrências, filtrando type=Expense no backend. */
@Component({
  selector: 'mf-recurring-expenses-report',
  standalone: true,
  imports: [CommonModule, ButtonDirective, ListStateComponent],
  templateUrl: './recurring-expenses-report.component.html',
  styleUrl: './recurring-expenses-report.component.scss',
})
export class RecurringExpensesReportComponent implements OnInit {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<RecurringTransaction[]>([]);
  readonly generatedAt = new Date();

  readonly monthlyTotal = computed(() =>
    this.items()
      .filter((i) => i.isActive)
      .reduce((sum, i) => sum + this.monthlyEquivalent(i), 0),
  );

  constructor(private readonly recurringService: RecurringService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.recurringService.getAll(TransactionType.Expense).subscribe({
      next: (items) => {
        this.items.set(items.filter((i) => i.isActive));
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar os gastos recorrentes.');
        this.loading.set(false);
      },
    });
  }

  frequencyLabel(frequency: RecurrenceFrequency): string {
    switch (frequency) {
      case RecurrenceFrequency.Weekly:
        return 'Semanal';
      case RecurrenceFrequency.Monthly:
        return 'Mensal';
      case RecurrenceFrequency.Yearly:
        return 'Anual';
      default:
        return String(frequency);
    }
  }

  /** Normaliza pra base mensal só pro total do relatório (semanal ×~4.33, anual ÷12). */
  private monthlyEquivalent(item: RecurringTransaction): number {
    switch (item.frequency) {
      case RecurrenceFrequency.Weekly:
        return item.currentAmount * 4.33;
      case RecurrenceFrequency.Yearly:
        return item.currentAmount / 12;
      default:
        return item.currentAmount;
    }
  }

  print(): void {
    window.print();
  }
}
