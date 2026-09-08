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
}

export interface UpdateCardRequest extends CreateCardRequest {
  isActive: boolean;
}
