/** Pagamento de fatura de cartão (docs/05 — Pagamento de cartão). */
export interface CardPayment {
  id: string;
  creditCardId: string;
  cycleClosingDate: string;
  paymentDate: string;
  amount: number;
  /** Null = pago por outra forma, sem sair de conta nenhuma. */
  accountId?: string | null;
  accountName?: string | null;
  paymentTransactionId?: string | null;
}

/**
 * A fatura de um ciclo, calculada a partir dos lançamentos. Não confundir com `CardInvoice`
 * (invoice.model.ts), que é o PDF importado do banco para conciliação.
 */
export interface CardInvoiceCycle {
  creditCardId: string;
  cardName: string;
  start: string;
  closingDate: string;
  dueDate: string;
  total: number;
  paid: number;
  outstanding: number;
  isClosed: boolean;
  isOverdue: boolean;
  payments: CardPayment[];
}

export interface CreateCardPaymentRequest {
  creditCardId: string;
  cycleClosingDate: string;
  paymentDate: string;
  amount: number;
  accountId?: string | null;
}

export interface UpdateCardPaymentRequest {
  paymentDate: string;
  amount: number;
  accountId?: string | null;
}
