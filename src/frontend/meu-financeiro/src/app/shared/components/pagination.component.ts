import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Paginator } from 'primeng/paginator';
import type { PaginatorState } from 'primeng/paginator';

/**
 * Paginação server-side reutilizável (regra 19, docs/14-angular.md), sobre p-paginator
 * (docs/migracao-primeng/00-plano.md, UI-01.1 — migrado de mat-paginator). A API externa
 * (page 1-based, totalPages, totalItems, pageChange) é preservada para não exigir mudanças
 * nos consumidores; o pageSize exibido é derivado (o backend não devolve o tamanho de
 * página explicitamente, só totalPages/totalItems). Tradução pt-BR (rótulos aria) é global,
 * ver app.config.ts (providePrimeNG) e shared/i18n/primeng-translation-pt-br.ts.
 */
@Component({
  selector: 'mf-pagination',
  standalone: true,
  imports: [CommonModule, Paginator],
  template: `
    @if (totalPages > 0) {
      <p-paginator
        [first]="firstRecord"
        [rows]="pageSizeEstimate"
        [totalRecords]="totalItems"
        [showFirstLastIcon]="true"
        [showCurrentPageReport]="true"
        currentPageReportTemplate="{first} – {last} de {totalRecords}"
        (onPageChange)="onPage($event)"
      />
    }
  `,
})
export class PaginationComponent {
  @Input({ required: true }) page = 1;
  @Input({ required: true }) totalPages = 0;
  @Input({ required: true }) totalItems = 0;
  @Output() pageChange = new EventEmitter<number>();

  get pageSizeEstimate(): number {
    return Math.max(1, Math.ceil(this.totalItems / Math.max(1, this.totalPages)));
  }

  get firstRecord(): number {
    return (this.page - 1) * this.pageSizeEstimate;
  }

  onPage(event: PaginatorState): void {
    const newPage = (event.page ?? 0) + 1;
    if (newPage !== this.page) this.pageChange.emit(newPage);
  }
}
