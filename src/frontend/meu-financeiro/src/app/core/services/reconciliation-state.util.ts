/**
 * Situação de um item na conciliação (extrato ou fatura), como a tela mostra.
 *
 * A importação já grava "Matched" quando a semelhança é alta, mas isso é SUGESTÃO: só vira
 * conciliado quando o usuário aceita (ou vincula à mão). Antes a tela dizia "Conciliado" para a
 * sugestão e mostrava "Aceitar" e "Desfazer" lado a lado — parecia feito e aceitável de novo.
 */
export interface ReconciliationLike {
  status: string;
  accepted?: boolean;
}

export type ReconciliationState = 'aceito' | 'sugerido' | 'possivel' | 'sem' | 'ignorado';

export function reconciliationState(item: ReconciliationLike): ReconciliationState {
  // Vínculo manual é sempre decisão do usuário (o aceite antigo, de antes do AcceptedAt, também).
  if (item.accepted || item.status === 'ManuallyMatched') return 'aceito';
  if (item.status === 'Matched') return 'sugerido';
  if (item.status === 'PossibleMatch') return 'possivel';
  if (item.status === 'Ignored') return 'ignorado';
  return 'sem';
}

export function isSuggestion(item: ReconciliationLike): boolean {
  const state = reconciliationState(item);
  return state === 'sugerido' || state === 'possivel';
}

export function reconciliationLabel(item: ReconciliationLike): string {
  switch (reconciliationState(item)) {
    case 'aceito': return 'Conciliado';
    case 'sugerido': return 'Sugerido';
    case 'possivel': return 'Possível';
    case 'ignorado': return 'Ignorado';
    default: return 'Sem correspondência';
  }
}

export function reconciliationBadge(item: ReconciliationLike): string {
  switch (reconciliationState(item)) {
    case 'aceito': return 'mf-badge-ok';
    case 'sugerido':
    case 'possivel': return 'mf-badge-warn';
    default: return 'mf-badge-neutral';
  }
}

/** Contagens do resumo: conciliado é só o aceito; sugestões contam "a revisar". */
export function reconciliationCounts(items: ReconciliationLike[]): { accepted: number; toReview: number; unmatched: number; percent: number } {
  const relevant = items.filter((i) => reconciliationState(i) !== 'ignorado');
  const accepted = relevant.filter((i) => reconciliationState(i) === 'aceito').length;
  return {
    accepted,
    toReview: relevant.filter(isSuggestion).length,
    unmatched: relevant.filter((i) => reconciliationState(i) === 'sem').length,
    percent: relevant.length === 0 ? 0 : Math.round((accepted / relevant.length) * 100),
  };
}
