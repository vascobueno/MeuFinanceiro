import { PaginationComponent } from './pagination.component';

describe('PaginationComponent', () => {
  it('deve estimar o tamanho de página a partir de totalItems/totalPages', () => {
    const component = new PaginationComponent();
    component.totalItems = 42;
    component.totalPages = 3;

    expect(component.pageSizeEstimate).toBe(14);
  });

  it('não deve emitir pageChange quando a página não muda', () => {
    const component = new PaginationComponent();
    component.page = 2;
    component.totalPages = 3;

    let emitted: number | null = null;
    component.pageChange.subscribe((p) => (emitted = p));

    component.onPage({ pageIndex: 1, pageSize: 10, length: 30 });

    expect(emitted).toBeNull();
  });

  it('deve emitir pageChange traduzindo o pageIndex (0-based) do mat-paginator para page (1-based)', () => {
    const component = new PaginationComponent();
    component.page = 1;
    component.totalPages = 3;

    let emitted: number | null = null;
    component.pageChange.subscribe((p) => (emitted = p));

    component.onPage({ pageIndex: 2, pageSize: 10, length: 30 });

    expect(emitted as number | null).toBe(3);
  });
});
