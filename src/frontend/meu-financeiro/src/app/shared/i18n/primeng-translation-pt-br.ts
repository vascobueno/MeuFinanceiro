import type { Translation } from 'primeng/api';

/**
 * Traduções pt-BR globais do PrimeNG (docs/migracao-primeng/00-plano.md, UI-01.1).
 * Registrado uma vez em app.config.ts (providePrimeNG) — substitui o antigo padrão de
 * MatPaginatorIntl por componente (mat-paginator-intl-pt-br.ts), já que a tradução do
 * PrimeNG é global por app, não por instância de componente.
 *
 * Preenchido incrementalmente conforme cada componente é migrado (Fase 1): só os campos
 * relevantes ao componente da vez, para não adivinhar rótulos de componentes ainda não
 * migrados.
 */
export const primeNgTranslationPtBr: Translation = {
  aria: {
    firstPageLabel: 'Primeira página',
    lastPageLabel: 'Última página',
    nextPageLabel: 'Próxima página',
    prevPageLabel: 'Página anterior',
    previousPageLabel: 'Página anterior',
    pageLabel: 'Página {page}',
    rowsPerPageLabel: 'Itens por página',
    jumpToPageDropdownLabel: 'Ir para a página',
    jumpToPageInputLabel: 'Ir para a página',
  },
};
