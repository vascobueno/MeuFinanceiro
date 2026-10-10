/**
 * Nome, para a tela, do tipo de movimento do extrato que o backend classifica
 * (StatementItemClassifier, docs/09). Tipo que esta lista não conheça aparece como veio, em vez
 * de sumir — o backend pode ganhar tipos antes da tela.
 */
const LABELS: Record<string, string> = {
  DEBIT_CARD_PURCHASE: 'Compra no débito',
  ATM_WITHDRAWAL: 'Saque',
  PIX_SENT: 'Pix enviado',
  PIX_RECEIVED: 'Pix recebido',
  TED_SENT: 'TED enviada',
  TED_RECEIVED: 'TED recebida',
  BOLETO_PAYMENT: 'Boleto',
  BILL_PAYMENT: 'Fatura de cartão',
  TAX_PAYMENT: 'Imposto',
  DIRECT_DEBIT: 'Débito automático',
  BANK_FEE: 'Tarifa',
  INTEREST_CHARGE: 'Juros',
  INVESTMENT: 'Aplicação',
  INVESTMENT_REDEMPTION: 'Resgate',
  INVESTMENT_INTEREST: 'Rendimento',
  TRANSFER: 'Transferência',
  OTHER_INCOME: 'Outra entrada',
  OTHER_EXPENSE: 'Outra saída',
};

export function statementItemTypeLabel(type: string | null | undefined): string {
  if (!type) return '';
  return LABELS[type] ?? type;
}
