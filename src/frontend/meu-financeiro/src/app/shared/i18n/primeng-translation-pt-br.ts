import type { Translation } from 'primeng/api';

/**
 * Traduções pt-BR globais do PrimeNG (docs/migracao-primeng/00-plano.md, UI-01.1).
 * Registrado uma vez em app.config.ts (providePrimeNG) — substitui o antigo padrão de
 * MatPaginatorIntl por componente (mat-paginator-intl-pt-br.ts), já que a tradução do
 * PrimeNG é global por app, não por instância de componente.
 *
 * Cobre as mensagens de lista vazia e de busca, o calendário e os botões genéricos — tudo o que
 * os componentes usados no app mostram por conta própria.
 */
export const primeNgTranslationPtBr: Translation = {
  // Listas, buscas e seletores: sem isto o PrimeNG mostra "No results found",
  // "No available options" etc. (autocompletar da descrição, filtro do seletor de banco).
  emptyMessage: 'Nenhuma opção disponível',
  emptySearchMessage: 'Nenhum resultado encontrado',
  emptyFilterMessage: 'Nenhum resultado encontrado',
  searchMessage: '{0} resultados disponíveis',
  selectionMessage: '{0} itens selecionados',
  emptySelectionMessage: 'Nenhum item selecionado',
  // Confirmações e botões genéricos.
  accept: 'Sim',
  reject: 'Não',
  choose: 'Escolher',
  upload: 'Enviar',
  cancel: 'Cancelar',
  clear: 'Limpar',
  apply: 'Aplicar',
  // Calendário (p-datepicker).
  dayNames: ['domingo', 'segunda-feira', 'terça-feira', 'quarta-feira', 'quinta-feira', 'sexta-feira', 'sábado'],
  dayNamesShort: ['dom', 'seg', 'ter', 'qua', 'qui', 'sex', 'sáb'],
  dayNamesMin: ['D', 'S', 'T', 'Q', 'Q', 'S', 'S'],
  monthNames: ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho', 'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro'],
  monthNamesShort: ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'],
  today: 'Hoje',
  weekHeader: 'Sem',
  firstDayOfWeek: 0,
  dateFormat: 'dd/mm/yy',
  weak: 'Fraca',
  medium: 'Média',
  strong: 'Forte',
  passwordPrompt: 'Digite uma senha',
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
