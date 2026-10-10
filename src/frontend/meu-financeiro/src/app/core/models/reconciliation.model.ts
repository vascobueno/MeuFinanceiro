export interface BankStatementItemRequest {
  date: string;
  description: string;
  document?: string;
  itemType: string;
  amount: number;
  /** Data da compra no débito, quando anterior à do lançamento no extrato. */
  effectiveDate?: string;
  /** FITID do OFX — o mesmo movimento em outro arquivo não entra de novo. */
  externalId?: string;
}

export interface ImportBankStatementRequest {
  accountId: string;
  referenceStart: string;
  referenceEnd: string;
  openingBalance: number;
  closingBalance: number;
  originalFileName?: string;
  items: BankStatementItemRequest[];
  /** Extrato que não fecha só entra com a confirmação explícita de quem importa. */
  confirmUnbalanced?: boolean;
}

/** Linha do texto colado que não pôde ser lida, com o motivo em português. */
export interface ImportLineError {
  line: number;
  content: string;
  message: string;
}

export interface ParseBankStatementRequest {
  text: string;
  referenceStart: string;
  referenceEnd: string;
}

/** O servidor lê o texto colado; os saldos vêm quando o texto trazia as linhas "SALDO EM". */
/** OFX lido pelo servidor: conta reconhecida pelos dados bancários, período, saldos e movimentos. */
export interface ParsedOfx {
  accountId: string | null;
  accountName: string | null;
  /** O que o arquivo diz da conta ("banco 033, agência 0001, conta 12345-6"). */
  accountFromFile: string;
  matchingAccounts: number;
  referenceStart: string | null;
  referenceEnd: string | null;
  openingBalance: number | null;
  closingBalance: number | null;
  items: BankStatementItemRequest[];
  errors: ImportLineError[];
  movementsSum: number;
  /** Movimentos com FITID já importado nesta conta — a importação os pula. */
  alreadyImported: number;
}

export interface ParsedBankStatement {
  items: BankStatementItemRequest[];
  errors: ImportLineError[];
  openingBalance: number | null;
  closingBalance: number | null;
  movementsSum: number;
}

export interface ParseCardInvoiceRequest {
  text: string;
  closingDate: string;
}

/** Cartão encontrado num cabeçalho da fatura; com issuedCardId quando o final está cadastrado. */
export interface InvoiceCardHeader {
  lastFour: string;
  firstDigits: string | null;
  holderName: string | null;
  /** "@" no cabeçalho: cartão virtual. */
  isVirtual: boolean;
  issuedCardId: string | null;
  creditCardId: string | null;
  creditCardName: string | null;
}

export interface ParsedCardInvoice {
  items: CardInvoiceItemRequest[];
  errors: ImportLineError[];
  itemsSum: number;
  cards: InvoiceCardHeader[] | null;
  /** O VALOR TOTAL da fatura, quando ela traz um só. */
  totalFromText: number | null;
  /** O grupo de cartões dos finais conhecidos — só se todos forem do mesmo. */
  detectedCreditCardId: string | null;
  detectedCreditCardName: string | null;
  /** Datas que a fatura imprime — usadas para oferecer o ajuste do fechamento do ciclo. */
  closingDateFromText: string | null;
  dueDateFromText: string | null;
  nextClosingDateFromText: string | null;
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
  /** Movimentos do arquivo que já estavam importados e ficaram de fora. */
  skippedDuplicates?: number;
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
  itemType: string;
  itemEffectiveDate?: string;
}

export interface CardInvoiceItemRequest {
  date: string;
  description: string;
  installmentLabel?: string;
  amount: number;
  // Preenchidos pela leitura do servidor; a tela só os repassa para a importação.
  cardLastFour?: string | null;
  kind?: string | number | null;
  foreignAmount?: number | null;
  foreignCurrency?: string | null;
  exchangeRate?: number | null;
  exchangeRateEstimated?: boolean;
  originalAmount?: number | null;
  originalCurrency?: string | null;
  relatedIndex?: number | null;
  installmentNumber?: number | null;
  installmentTotal?: number | null;
}

export interface ImportCardInvoiceRequest {
  creditCardId: string;
  closingDate: string;
  dueDate: string;
  totalAmount: number;
  originalFileName?: string;
  items: CardInvoiceItemRequest[];
  /** Saldo da fatura anterior: a fatura fecha como saldo anterior + itens = total. */
  previousBalance?: number;
  confirmUnbalanced?: boolean;
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
  /** Pagamento de fatura ligado à linha "PAGAMENTO" — alternativa a financialTransactionId. */
  cardPaymentId?: string;
  status: string;
  score: number;
  amountDifference: number;
  dateDifferenceDays: number;
  /** Com o quê o item foi ligado ("Parcela 4/6 de …", "Pagamento de …") ou por que não foi. */
  reason?: string;
  /** Aceito pelo usuário — Matched sozinho pode ser só a sugestão da importação. */
  accepted: boolean;
  itemCardLastFour?: string | null;
  /** Expense, Installment, Payment, Credit, Fee, Tax. */
  itemKind?: string | null;
  itemForeignAmount?: number | null;
  itemForeignCurrency?: string | null;
  itemExchangeRate?: number | null;
  itemExchangeRateEstimated?: boolean;
  itemOriginalAmount?: number | null;
  itemOriginalCurrency?: string | null;
  itemIssuedCardId?: string | null;
  itemRelatedItemId?: string | null;
}
