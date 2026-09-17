export enum AccountType {
  Checking = 1,
  Savings = 2,
}

export interface Account {
  id: string;
  name: string;
  bank?: string;
  type: AccountType;
  initialBalance: number;
  currentBalance: number;
  isActive: boolean;
}

export interface CreateAccountRequest {
  name: string;
  bank?: string;
  type: AccountType;
  initialBalance: number;
}

export interface UpdateAccountRequest {
  name: string;
  bank?: string;
  type: AccountType;
  initialBalance: number;
  isActive: boolean;
}
