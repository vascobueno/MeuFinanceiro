import { RecurrenceFrequency, TransactionType } from './transaction.model';

/** Prévia de uma ocorrência futura ainda não materializada (mesmo cálculo da Projeção). */
export interface RecurringOccurrencePreview {
  date: string;
  amount: number;
}

/** Espelha RecurringTransactionResponse da API (docs/screens/recorrencia). */
export interface RecurringTransaction {
  id: string;
  description: string;
  type: TransactionType;
  currentAmount: number;
  frequency: RecurrenceFrequency;
  accountId?: string | null;
  accountName?: string | null;
  creditCardId?: string | null;
  creditCardName?: string | null;
  categoryId?: string | null;
  startDate: string;
  endDate?: string | null;
  isActive: boolean;
  nextOccurrences: RecurringOccurrencePreview[];
}

/**
 * Linha do relatório imprimível de gastos recorrentes. Shape enxuto de propósito: o relatório
 * precisa de todos os itens ativos (o total tem de fechar), mas não usa origem nem as 3 próximas
 * ocorrências — só a primeira data.
 */
export interface RecurringExpenseReportItem {
  id: string;
  description: string;
  frequency: RecurrenceFrequency;
  currentAmount: number;
  nextDate?: string | null;
}

/** Regra 13: cria nova versão por data de vigência; histórico permanece intacto. */
export interface CreateRecurringVersionRequest {
  effectiveFrom: string;
  amount: number;
}

/** Altera o cadastro (destino/categoria/vigência/ativa) — o valor só muda por versão. */
export interface UpdateRecurringTransactionRequest {
  description: string;
  accountId?: string | null;
  creditCardId?: string | null;
  categoryId?: string | null;
  endDate?: string | null;
  isActive: boolean;
}
