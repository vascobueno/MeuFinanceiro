import { PagedRequest } from './paged-result.model';

/**
 * Extrato bancário importado (docs/09-conciliacao-conta-corrente.md). É o espelho de
 * CardInvoice do lado da conta: `netChange` e `itemCount` vêm calculados do servidor.
 */
export interface BankStatement {
  id: string;
  accountId: string;
  accountName: string;
  referenceStart: string;
  referenceEnd: string;
  openingBalance: number;
  closingBalance: number;
  netChange: number;
  itemCount: number;
  originalFileName: string | null;
  /** Saldo inicial + movimentos bateu com o saldo final na importação. */
  balanceValidated: boolean;
}

export interface BankStatementFilter extends PagedRequest {
  accountId?: string | null;
  periodStart?: string | null;
  periodEnd?: string | null;
  minAmount?: number | null;
  maxAmount?: number | null;
}
