/** Pagamento de fatura de cartão (docs/05 — Pagamento de cartão). */
export interface CardPayment {
  id: string;
  creditCardId: string;
  /** Mês da fatura que este pagamento quita (dia 1) — a identidade do ciclo. */
  cycleReference: string;
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
  /** Mês de referência (dia 1). É por ele que se navega e se aloca pagamento. */
  reference: string;
  start: string;
  closingDate: string;
  dueDate: string;
  total: number;
  paid: number;
  outstanding: number;
  isClosed: boolean;
  isOverdue: boolean;
  /** O fechamento deste mês foi ajustado (difere do dia do cadastro). */
  isClosingOverridden: boolean;
  /** O fechamento que o cadastro do cartão daria — para o "voltar ao padrão". */
  defaultClosingDate: string;
  /** O vencimento deste mês foi ajustado. */
  isDueOverridden: boolean;
  /** O vencimento derivado do fechamento padrão — para o "voltar ao padrão". */
  defaultDueDate: string;
  payments: CardPayment[];
}

export interface CreateCardPaymentRequest {
  creditCardId: string;
  cycleReference: string;
  paymentDate: string;
  amount: number;
  accountId?: string | null;
}

export interface UpdateCardPaymentRequest {
  paymentDate: string;
  amount: number;
  accountId?: string | null;
}
