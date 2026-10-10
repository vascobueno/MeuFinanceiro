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
  bankCode?: string | null;
  agency?: string | null;
  agencyDigit?: string | null;
  accountNumber?: string | null;
  accountDigit?: string | null;
  /** Limite do cheque especial — fora do saldo e do "Saldo total". */
  overdraftLimit: number;
  /** Saldo + limite. */
  availableBalance: number;
}

/** Dados bancários e limite: opcionais no envio; o backend normaliza e valida (todos ou nenhum). */
export interface AccountBankFields {
  bankCode?: string | null;
  agency?: string | null;
  agencyDigit?: string | null;
  accountNumber?: string | null;
  accountDigit?: string | null;
  overdraftLimit?: number;
}

export interface CreateAccountRequest extends AccountBankFields {
  name: string;
  bank?: string;
  type: AccountType;
  initialBalance: number;
}

export interface UpdateAccountRequest extends AccountBankFields {
  name: string;
  bank?: string;
  type: AccountType;
  initialBalance: number;
  isActive: boolean;
}
