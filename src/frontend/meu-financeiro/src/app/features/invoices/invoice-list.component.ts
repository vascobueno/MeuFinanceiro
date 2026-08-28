import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule, MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatSortModule, Sort } from '@angular/material/sort';
import { InvoiceService } from '../../core/services/invoice.service';
import { CardService } from '../../core/services/card.service';
import { CardInvoice, CardInvoiceFilter, CardInvoiceStatus } from '../../core/models/invoice.model';
import { CreditCard } from '../../core/models/card.model';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { PaginationComponent } from '../../shared/components/pagination.component';
import { DateRange, DateRangeComponent } from '../../shared/components/date-range.component';

/** Tela Faturas, espelhando docs/screens/faturas/mockup.html. Filtros/ordenação/paginação server-side (regra 19). */
@Component({
  selector: 'mf-invoice-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatSortModule,
    ListStateComponent,
    PaginationComponent,
    DateRangeComponent,
  ],
  providers: [{ provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { floatLabel: 'always', subscriptSizing: 'dynamic' } }],
  templateUrl: './invoice-list.component.html',
  styleUrl: './invoice-list.component.scss',
})
export class InvoiceListComponent implements OnInit {
  readonly CardInvoiceStatus = CardInvoiceStatus;
  readonly displayedColumns = ['cartao', 'periodo', 'fechamento', 'vencimento', 'total', 'status'];

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly invoices = signal<CardInvoice[]>([]);
  readonly totalPages = signal(0);
  readonly totalItems = signal(0);

  readonly cards = signal<CreditCard[]>([]);
  readonly period: DateRange = { start: null, end: null };

  readonly filter: CardInvoiceFilter = {
    page: 1,
    pageSize: 15,
    sortBy: 'fechamento',
    sortDir: 'desc',
    creditCardId: null,
    periodStart: null,
    periodEnd: null,
    status: null,
    minAmount: null,
    maxAmount: null,
  };

  constructor(
    private readonly invoiceService: InvoiceService,
    private readonly cardService: CardService,
  ) {}

  ngOnInit(): void {
    this.cardService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => this.cards.set(r.items));
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.invoiceService.getAll(this.filter).subscribe({
      next: (result) => {
        this.invoices.set(result.items);
        this.totalPages.set(result.totalPages);
        this.totalItems.set(result.totalItems);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar as faturas.');
        this.loading.set(false);
      },
    });
  }

  applyFilters(): void {
    this.filter.page = 1;
    this.load();
  }

  onPeriodChange(range: DateRange): void {
    this.period.start = range.start;
    this.period.end = range.end;
    this.filter.periodStart = range.start;
    this.filter.periodEnd = range.end;
    this.applyFilters();
  }

  onPageChange(page: number): void {
    this.filter.page = page;
    this.load();
  }

  onSortChange(sort: Sort): void {
    this.filter.sortBy = sort.active;
    this.filter.sortDir = sort.direction === 'desc' ? 'desc' : 'asc';
    this.load();
  }
}
