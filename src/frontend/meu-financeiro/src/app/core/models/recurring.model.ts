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
  accountName?: string | null;
  creditCardName?: string | null;
  startDate: string;
  endDate?: string | null;
  isActive: boolean;
  nextOccurrences: RecurringOccurrencePreview[];
}

/** Regra 13: cria nova versão por data de vigência; histórico permanece intacto. */
export interface CreateRecurringVersionRequest {
  effectiveFrom: string;
  amount: number;
}
