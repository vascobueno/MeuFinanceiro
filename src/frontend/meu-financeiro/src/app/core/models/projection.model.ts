import { TransactionType } from './transaction.model';

export interface ProjectionEntry {
  date: string;
  description: string;
  amount: number;
  isProjected: boolean;
  type: TransactionType;
}

export interface Projection {
  date: string;
  currentAccountsBalance: number;
  projectedAccountsBalance: number;
  investmentsBalance: number;
  projectedNetWorth: number;
  hasNegativeAccountsBalance: boolean;
  timeline: ProjectionEntry[];
  /** Agregados da linha COMPLETA — válidos mesmo quando `timeline` vem truncada. */
  timelineCount: number;
  projectedExpensesTotal: number;
}
