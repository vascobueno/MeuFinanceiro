import { Transaction } from '../../core/models/transaction.model';

/** O que a coluna Cartão mostra num lançamento: o cartão cadastrado, o final da fatura ou nada. */
export interface IssuedCardCell {
  text: string;
  /** Dica ao passar o mouse; nula quando o texto já basta. */
  hint: string | null;
  source: 'cadastrado' | 'fatura' | 'nenhum';
}

const KIND: Record<number, string> = { 1: 'Titular', 2: 'Adic.', 3: 'Virtual' };

export function issuedCardCell(
  t: Pick<Transaction, 'issuedCardLastFour' | 'issuedCardKind' | 'invoiceCardLastFour'>,
): IssuedCardCell {
  if (t.issuedCardLastFour) {
    const kind = t.issuedCardKind ? KIND[t.issuedCardKind] : null;
    return { text: `${kind ? kind + ' ' : ''}•••• ${t.issuedCardLastFour}`, hint: null, source: 'cadastrado' };
  }
  if (t.invoiceCardLastFour) {
    return {
      text: `•••• ${t.invoiceCardLastFour}`,
      hint: 'Final lido da fatura; o cartão não está cadastrado no grupo',
      source: 'fatura',
    };
  }
  return { text: 'Não informado', hint: null, source: 'nenhum' };
}
