export enum TransactionType {
  Income = 1,
  Expense = 2,
  Transfer = 3,
  InvestmentApplication = 4,
  InvestmentRedemption = 5,
}

export enum TransactionDirection {
  Debit = 1,
  Credit = 2,
}

export enum TransactionStatus {
  Pending = 1,
  Confirmed = 2,
  Reconciled = 3,
}

export enum InstallmentMode {
  TotalAmount = 1,
  InstallmentAmount = 2,
}

export enum RecurrenceFrequency {
  Weekly = 1,
  Monthly = 2,
  Yearly = 3,
}

export interface InstallmentRequest {
  mode: InstallmentMode;
  amount: number;
  installmentCount: number;
  firstDueDate: string;
}

export interface RecurringRequest {
  frequency: RecurrenceFrequency;
  endDate?: string | null;
}

export interface CreateTransactionRequest {
  transactionDate: string;
  description: string;
  categoryId?: string | null;
  subcategoryId?: string | null;
  accountId?: string | null;
  creditCardId?: string | null;
  destinationAccountId?: string | null;
  type: TransactionType;
  direction: TransactionDirection;
  amount: number;
  notes?: string | null;
  installment?: InstallmentRequest | null;
  recurring?: RecurringRequest | null;
}

export interface UpdateTransactionRequest {
  transactionDate: string;
  description: string;
  categoryId?: string | null;
  subcategoryId?: string | null;
  accountId?: string | null;
  creditCardId?: string | null;
  amount: number;
  direction: TransactionDirection;
  status: TransactionStatus;
  notes?: string | null;
}

export interface Transaction {
  id: string;
  transactionDate: string;
  description: string;
  merchantId?: string;
  categoryId?: string;
  categoryName?: string;
  subcategoryId?: string;
  accountId?: string;
  accountName?: string;
  creditCardId?: string;
  creditCardName?: string;
  type: TransactionType;
  direction: TransactionDirection;
  amount: number;
  status: TransactionStatus;
  installmentId?: string;
  installmentNumber?: number;
  installmentCount?: number;
  /** Plano da parcela — é nele que fica o anexo do parcelamento. */
  installmentPlanId?: string;
  recurringTransactionId?: string;
  /** Quantos anexos; numa parcela, inclui os do plano. Só para o clipe na lista. */
  attachmentCount?: number;
  notes?: string;
}

/** De onde o lançamento saiu — as duas telas de Lançamentos (docs/06). */
export enum TransactionOrigin {
  Account = 1,
  Card = 2,
}

export interface TransactionFilter {
  page: number;
  pageSize: number;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  search?: string;
  startDate?: string | null;
  endDate?: string | null;
  categoryId?: string | null;
  accountId?: string | null;
  creditCardId?: string | null;
  /** "de qualquer conta" / "de qualquer cartão" — os filtros por id acima são "desta/deste". */
  origin?: TransactionOrigin | null;
  type?: TransactionType | null;
  status?: TransactionStatus | null;
  minAmount?: number | null;
  maxAmount?: number | null;
}
