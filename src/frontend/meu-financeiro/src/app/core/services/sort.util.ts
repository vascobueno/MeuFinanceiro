export type SortDir = 'asc' | 'desc';

export interface SortState {
  sortBy?: string | null;
  sortDir?: string | null;
}

/**
 * Próxima ordenação ao clicar numa coluna da grade.
 *
 * A direção é decidida AQUI, e não pelo `order` que o `p-table` emite, porque a tabela não
 * sobrevive à carga: ela vive dentro de um `@if (!loading() && ...)`, some do DOM enquanto a
 * requisição acontece e volta como componente novo. Sem estado interno, o PrimeNG compara o
 * campo clicado com um `sortField` sempre vazio e cai no `defaultSortOrder` — isto é, emite
 * `order = 1` em todo clique, e a lista nunca invertia.
 *
 * Medido: dois cliques seguidos na mesma coluna mandavam `sortDir=asc` as duas vezes.
 */
export function nextSort(current: SortState, field: string | undefined | null, fallback: string): { sortBy: string; sortDir: SortDir } {
  const sortBy = field ?? fallback;
  // Mesmo campo e já ascendente: inverte. Campo novo (ou já descendente): começa ascendente.
  const sortDir: SortDir = current.sortBy === sortBy && current.sortDir === 'asc' ? 'desc' : 'asc';
  return { sortBy, sortDir };
}
