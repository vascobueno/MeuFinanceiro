/** Compra no exterior: valor e moeda da cobrança, cotação e, se houver, moeda de origem. */
export interface ForeignPurchase {
  amount: number;
  currency: string;
  exchangeRate?: number | null;
  originalAmount?: number | null;
  originalCurrency?: string | null;
}

const SYMBOLS: Record<string, string> = { USD: 'US$', EUR: '€', GBP: '£' };

function money(value: number): string {
  return value.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/** "US$ 21,22 × 5,4970 · origem EUR 19,50" — a linha que acompanha o valor em reais. */
export function formatForeign(f: ForeignPurchase | null | undefined, estimated = false): string {
  if (!f) return '';
  let line = `${SYMBOLS[f.currency] ?? f.currency} ${money(f.amount)}`;
  if (f.exchangeRate != null) {
    line += ` × ${f.exchangeRate.toLocaleString('pt-BR', { minimumFractionDigits: 4, maximumFractionDigits: 6 })}`;
    if (estimated) line += ' (calculada)';
  }
  if (f.originalAmount != null && f.originalCurrency) line += ` · origem ${f.originalCurrency} ${money(f.originalAmount)}`;
  return line;
}

/** Valor em reais pelo estrangeiro e a cotação, em centavos — para preencher o valor quando vazio. */
export function brlFromForeign(amount: number | null | undefined, rate: number | null | undefined): number | null {
  if (!amount || !rate) return null;
  return Math.round(amount * rate * 100) / 100;
}
