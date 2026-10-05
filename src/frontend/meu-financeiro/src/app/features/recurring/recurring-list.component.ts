import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { ConfirmationService, SortEvent } from 'primeng/api';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { Table, SortIcon, SortableColumn } from 'primeng/table';
import { Tooltip } from 'primeng/tooltip';
import { PaginationComponent } from '../../shared/components/pagination.component';
import { AuthService } from '../../core/services/auth.service';
import { RecurringService } from '../../core/services/recurring.service';
import { RecurringTransaction } from '../../core/models/recurring.model';
import { RecurrenceFrequency, TransactionType } from '../../core/models/transaction.model';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { nextSort } from '../../core/services/sort.util';

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
  imports: [
    CommonModule,
    RouterLink,
    MfIconComponent,
    Table,
    SortIcon,
    SortableColumn,
    ListStateComponent,
    PaginationComponent,
    Tooltip,
    ConfirmDialog,
  ],
  providers: [ConfirmationService],
  templateUrl: './recurring-list.component.html',
  styleUrl: './recurring-list.component.scss',
})
export class RecurringListComponent implements OnInit {
  readonly TransactionType = TransactionType;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly recurrences = signal<RecurringTransaction[]>([]);
  readonly totalPages = signal(0);
  readonly totalItems = signal(0);

  // Objeto mutável, não signal — mesmo padrão de invoice-list/transaction-list.
  readonly filter = { page: 1, pageSize: 20, sortBy: undefined as string | undefined, sortDir: 'asc' as 'asc' | 'desc' };

  constructor(
    private readonly recurringService: RecurringService,
    private readonly confirmationService: ConfirmationService,
    readonly authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.recurringService.getAll(this.filter).subscribe({
      next: (result) => {
        this.recurrences.set(result.items);
        this.totalPages.set(result.totalPages);
        this.totalItems.set(result.totalItems);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar as recorrências.');
        this.loading.set(false);
      },
    });
  }

  onSort(event: SortEvent): void {
    // A direção vem de nextSort, e não do `order` do evento: a tabela é recriada a cada carga
    // e o p-table emite sempre ascendente (core/services/sort.util.ts).
    const proxima = nextSort(this.filter, event.field, 'descricao');
    this.filter.sortBy = proxima.sortBy;
    this.filter.sortDir = proxima.sortDir;
    this.load();
  }

  onPageChange(page: number): void {
    this.filter.page = page;
    this.load();
  }

  confirmDelete(recurring: RecurringTransaction): void {
    this.confirmationService.confirm({
      header: 'Excluir recorrência',
      message: `Deseja realmente excluir '${recurring.description}'? Ocorrências já conciliadas continuam no histórico como lançamentos avulsos.`,
      acceptLabel: 'Excluir',
      rejectLabel: 'Cancelar',
      accept: () => this.delete(recurring),
    });
  }

  private delete(recurring: RecurringTransaction): void {
    this.recurringService.delete(recurring.id).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Não foi possível excluir a recorrência.'),
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
