import { Component, OnInit, ViewChild, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { AuthService } from '../../core/services/auth.service';
import { RecurringService } from '../../core/services/recurring.service';
import { RecurringTransaction } from '../../core/models/recurring.model';
import { RecurrenceFrequency, TransactionType } from '../../core/models/transaction.model';
import { ListStateComponent } from '../../shared/components/list-state.component';

/**
 * Tela Recorrências: lista lançamentos recorrentes cadastrados (regra 13).
 * Lista completa carregada de uma vez — ordenação por coluna é feita no cliente via
 * MatTableDataSource + MatSort.
 */
@Component({
  selector: 'mf-recurring-list',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatIconModule, MatTableModule, MatSortModule, ListStateComponent],
  templateUrl: './recurring-list.component.html',
  styleUrl: './recurring-list.component.scss',
})
export class RecurringListComponent implements OnInit {
  readonly TransactionType = TransactionType;
  readonly displayedColumns = [
    'description',
    'type',
    'origin',
    'frequency',
    'amount',
    'startDate',
    'nextOccurrences',
    'status',
    'actions',
  ];

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly recurrences = signal<RecurringTransaction[]>([]);
  readonly dataSource = new MatTableDataSource<RecurringTransaction>([]);

  // Setter (não ngAfterViewInit) — a <table> só existe depois que loading() vira false, então
  // um @ViewChild lido em ngAfterViewInit pegaria undefined; o setter dispara de novo quando ela aparece.
  @ViewChild(MatSort) set sort(sort: MatSort) {
    if (sort) this.dataSource.sort = sort;
  }

  constructor(
    private readonly recurringService: RecurringService,
    readonly authService: AuthService,
  ) {
    // "origin" e "frequency" exibem texto combinado/traduzido — ordenar pelo valor real.
    this.dataSource.sortingDataAccessor = (item, property) => {
      switch (property) {
        case 'origin':
          return item.accountName || item.creditCardName || '';
        case 'frequency':
          return item.frequency;
        default:
          return item[property as keyof RecurringTransaction] as string | number;
      }
    };
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.recurringService.getAll().subscribe({
      next: (recurrences) => {
        this.recurrences.set(recurrences);
        this.dataSource.data = recurrences;
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
