import { IssuedCardRequest } from './issued-card.model';

export interface CreditCard {
  id: string;
  name: string;
  brand?: string;
  closingDay: number;
  dueDay: number;
  limit?: number;
  isActive: boolean;
}

export interface CreateCardRequest {
  name: string;
  brand?: string;
  closingDay: number;
  dueDay: number;
  limit?: number;
  /** Cartão do titular, criado junto com o grupo na mesma gravação. */
  titular?: IssuedCardRequest | null;
}

export interface UpdateCardRequest extends Omit<CreateCardRequest, 'titular'> {
  isActive: boolean;
}
