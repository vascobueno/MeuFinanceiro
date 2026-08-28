export interface BankStatementItemRequest {
  date: string;
  description: string;
  document?: string;
  itemType: string;
  amount: number;
}

export interface ImportBankStatementRequest {
  accountId: string;
  referenceStart: string;
  referenceEnd: string;
  openingBalance: number;
  closingBalance: number;
  originalFileName?: string;
  items: BankStatementItemRequest[];
}

export interface BankStatement {
  id: string;
  accountId: string;
  accountName: string;
  referenceStart: string;
  referenceEnd: string;
  openingBalance: number;
  closingBalance: number;
  balanceValidated: boolean;
  itemCount: number;
}

export interface BankReconciliationItem {
  id: string;
  bankStatementItemId: string;
  itemDate: string;
  itemDescription: string;
  itemAmount: number;
  financialTransactionId?: string;
  status: string;
  score: number;
  amountDifference: number;
  dateDifferenceDays: number;
  reason?: string;
}

export interface CardInvoiceItemRequest {
  date: string;
  description: string;
  installmentLabel?: string;
  amount: number;
}

export interface ImportCardInvoiceRequest {
  creditCardId: string;
  closingDate: string;
  dueDate: string;
  totalAmount: number;
  originalFileName?: string;
  items: CardInvoiceItemRequest[];
}

export interface CardInvoice {
  id: string;
  creditCardId: string;
  creditCardName: string;
  closingDate: string;
  dueDate: string;
  totalAmount: number;
  totalValidated: boolean;
  itemCount: number;
}

export interface CardReconciliationItem {
  id: string;
  cardInvoiceItemId: string;
  itemDate: string;
  itemDescription: string;
  itemAmount: number;
  financialTransactionId?: string;
  status: string;
  score: number;
  amountDifference: number;
  dateDifferenceDays: number;
  reason?: string;
}
