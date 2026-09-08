export enum InvestmentTransactionType {
  Application = 1,
  Redemption = 2,
  Yield = 3,
}

export interface InvestmentAccount {
  id: string;
  institution: string;
  name?: string;
  totalPosition: number;
}

export interface Investment {
  id: string;
  investmentAccountId: string;
  name: string;
  type?: string;
  maturityDate?: string;
  liquidity?: string;
  principal: number;
  netValue: number;
}

export interface InvestmentsSummary {
  totalPosition: number;
  accounts: InvestmentAccount[];
}

export interface CreateInvestmentAccountRequest {
  institution: string;
  name?: string;
}

export interface CreateInvestmentRequest {
  investmentAccountId: string;
  name: string;
  type?: string;
  maturityDate?: string | null;
  liquidity?: string;
}

export interface CreateInvestmentTransactionRequest {
  investmentId: string;
  type: InvestmentTransactionType;
  date: string;
  amount: number;
  accountId?: string | null;
}
