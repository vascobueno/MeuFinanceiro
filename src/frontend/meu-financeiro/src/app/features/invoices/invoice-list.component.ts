import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { Table, SortIcon, SortableColumn } from 'primeng/table';
import { SortEvent } from 'primeng/api';
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
    MfIconComponent,
    InputText,
    Select,
    Table,
    SortIcon,
    SortableColumn,
    ListStateComponent,
    PaginationComponent,
    DateRangeComponent,
  ],
  templateUrl: './invoice-list.component.html',
  styleUrl: './invoice-list.component.scss',
})
export class InvoiceListComponent implements OnInit {
  readonly CardInvoiceStatus = CardInvoiceStatus;
  readonly statusOptions = [
    { label: 'Todos', value: null },
    { label: 'Aberta', value: CardInvoiceStatus.Aberta },
    { label: 'Vencida', value: CardInvoiceStatus.Vencida },
  ];

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly invoices = signal<CardInvoice[]>([]);
  readonly totalPages = signal(0);
  readonly totalItems = signal(0);

  readonly cards = signal<CreditCard[]>([]);
  readonly cardOptions = computed(() => [{ label: 'Todos', value: null }, ...this.cards().map((c) => ({ label: c.name, value: c.id }))]);
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

  onSort(event: SortEvent): void {
    this.filter.sortBy = event.field ?? 'fechamento';
    this.filter.sortDir = event.order === -1 ? 'desc' : 'asc';
    this.load();
  }
}
