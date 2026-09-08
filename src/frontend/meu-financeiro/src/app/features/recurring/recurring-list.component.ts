import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { Table, SortIcon, SortableColumn } from 'primeng/table';
import { Tooltip } from 'primeng/tooltip';
import { AuthService } from '../../core/services/auth.service';
import { RecurringService } from '../../core/services/recurring.service';
import { RecurringTransaction } from '../../core/models/recurring.model';
import { RecurrenceFrequency, TransactionType } from '../../core/models/transaction.model';
import { ListStateComponent } from '../../shared/components/list-state.component';

/**
 * Tela Recorrências: lista lançamentos recorrentes cadastrados (regra 13).
 * Lista completa carregada de uma vez — ordenação por coluna é feita no cliente pelo
 * próprio p-table (sem [customSort]/(onSort), direto no campo real de cada coluna). A
 * coluna "Origem" combina accountName/creditCardName (sem um único campo real por trás) e
 * por isso fica sem `pSortableColumn` — mesma lógica que levou a Fase 3 a substituir o
 * `sortingDataAccessor` do MatTableDataSource por ordenação direta pelo campo real.
 */
@Component({
  selector: 'mf-recurring-list',
  standalone: true,
  imports: [CommonModule, RouterLink, MfIconComponent, Table, SortIcon, SortableColumn, ListStateComponent, Tooltip],
  templateUrl: './recurring-list.component.html',
  styleUrl: './recurring-list.component.scss',
})
export class RecurringListComponent implements OnInit {
  readonly TransactionType = TransactionType;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly recurrences = signal<RecurringTransaction[]>([]);

  constructor(
    private readonly recurringService: RecurringService,
    readonly authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.recurringService.getAll().subscribe({
      next: (recurrences) => {
        this.recurrences.set(recurrences);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar as recorrências.');
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
        return '—';
    }
  }
}
