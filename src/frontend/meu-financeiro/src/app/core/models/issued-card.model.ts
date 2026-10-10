/** Os cartões de um grupo (fatura): titular, adicionais e virtuais. Espelha IssuedCardKind. */
export enum IssuedCardKind {
  Titular = 1,
  Adicional = 2,
  Online = 3,
}

/** "Vencido" não é status: vem calculado em isExpired. */
export enum IssuedCardStatus {
  Ativo = 1,
  Substituido = 2,
  Cancelado = 3,
}

/** Calculada pelas ligações: grupo de crédito, conta de débito ou as duas (múltiplo). */
export enum IssuedCardFunction {
  Credito = 1,
  Debito = 2,
  Multiplo = 3,
}

export interface IssuedCard {
  id: string;
  creditCardId: string | null;
  kind: IssuedCardKind;
  firstDigits: string | null;
  lastFour: string;
  expiryMonth: number | null;
  expiryYear: number | null;
  holderName: string | null;
  status: IssuedCardStatus;
  isExpired: boolean;
  replacedById: string | null;
  /** A bandeira que os primeiros dígitos indicam, quando não há dúvida. */
  suggestedBrand: string | null;
  /** Os dígitos indicam outra bandeira que a do grupo — aviso, não bloqueio. */
  brandMismatch: boolean;
  function: IssuedCardFunction;
  accountId: string | null;
  accountName: string | null;
  creditCardName: string | null;
  /** A bandeira que vale: a do grupo no crédito, a própria no só de débito. */
  brand: string | null;
}

/** Sem campo de número completo: ver core/services/card-number.util.ts. */
export interface IssuedCardRequest {
  kind: IssuedCardKind;
  lastFour: string;
  firstDigits?: string | null;
  expiryMonth?: number | null;
  expiryYear?: number | null;
  holderName?: string | null;
  /** Conta do débito: no cartão do grupo, torna-o múltiplo; no só de débito, obrigatória. */
  accountId?: string | null;
  /** Só no cartão só de débito. */
  brand?: string | null;
}
