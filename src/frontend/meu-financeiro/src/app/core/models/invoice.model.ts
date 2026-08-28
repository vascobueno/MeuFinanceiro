import { PagedRequest } from './paged-result.model';

/**
 * Espelha CardInvoiceStatus da API — status derivado da data de vencimento (docs/screens/faturas).
 * Numérico (0/1), igual à convenção dos demais enums do app (a API serializa enums como número).
 */
export enum CardInvoiceStatus {
  Aberta = 0,
  Vencida = 1,
}

export interface CardInvoice {
  id: string;
  creditCardId: string;
  creditCardName: string;
  closingDate: string;
  dueDate: string;
  totalAmount: number;
  status: CardInvoiceStatus;
}

export interface CardInvoiceFilter extends PagedRequest {
  creditCardId?: string | null;
  periodStart?: string | null;
  periodEnd?: string | null;
  status?: CardInvoiceStatus | null;
  minAmount?: number | null;
  maxAmount?: number | null;
}
