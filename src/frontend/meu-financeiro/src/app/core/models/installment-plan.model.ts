import { InstallmentMode } from './transaction.model';

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
  totalAmount: number;
  installmentCount: number;
  installmentAmounts: number[];
}
