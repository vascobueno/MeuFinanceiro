import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatPaginatorIntl, MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { providePaginatorIntlPtBr } from '../i18n/mat-paginator-intl-pt-br';

/**
 * Paginação server-side reutilizável (regra 19, docs/14-angular.md), sobre mat-paginator.
 * A API externa (page 1-based, totalPages, totalItems, pageChange) é preservada para não
 * exigir mudanças nos consumidores; o pageSize exibido é derivado (o backend não devolve
 * o tamanho de página explicitamente, só totalPages/totalItems).
 */
@Component({
  selector: 'mf-pagination',
  standalone: true,
  imports: [CommonModule, MatPaginatorModule],
  providers: [{ provide: MatPaginatorIntl, useFactory: providePaginatorIntlPtBr }],
  template: `
    @if (totalPages > 0) {
      <mat-paginator
        [length]="totalItems"
        [pageSize]="pageSizeEstimate"
        [pageIndex]="page - 1"
        [hidePageSize]="true"
        [showFirstLastButtons]="true"
        (page)="onPage($event)"
      ></mat-paginator>
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

  onPage(event: PageEvent): void {
    const newPage = event.pageIndex + 1;
    if (newPage !== this.page) this.pageChange.emit(newPage);
  }
}
