import { InstallmentMode, TransactionStatus } from './transaction.model';

/** Tela Parcelamento (docs/screens/parcelamento). */
export interface CreateInstallmentPlanRequest {
  description: string;
  accountId?: string | null;
  creditCardId?: string | null;
  categoryId?: string | null;
  mode: InstallmentMode;
  amount: number;
  installmentCount: number;
  firstDueDate: string;
}

export interface InstallmentPlanResult {
  /** Id do plano criado — usado para anexar arquivos logo após salvar. */
  id: string;
  totalAmount: number;
  installmentCount: number;
  installmentAmounts: number[];
}

/**
 * Linha da lista de parcelamentos. Origem e andamento vêm dos lançamentos gerados por cada
 * parcela — o plano em si não guarda conta/cartão.
 */
export interface InstallmentPlanListItem {
  id: string;
  description: string;
  mode: InstallmentMode;
  totalAmount: number;
  installmentCount: number;
  paidCount: number;
  accountName?: string | null;
  creditCardName?: string | null;
  firstDueDate: string;
  lastDueDate: string;
  nextInstallmentNumber?: number | null;
  nextDueDate?: string | null;
  nextAmount?: number | null;
}

export interface InstallmentItem {
  id: string;
  number: number;
  amount: number;
  dueDate: string;
  status?: TransactionStatus | null;
  financialTransactionId?: string | null;
}

export interface InstallmentPlanDetail {
  id: string;
  description: string;
  mode: InstallmentMode;
  totalAmount: number;
  installmentCount: number;
  paidCount: number;
  accountName?: string | null;
  creditCardName?: string | null;
  installments: InstallmentItem[];
}
