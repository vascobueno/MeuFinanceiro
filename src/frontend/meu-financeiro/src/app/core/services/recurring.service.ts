import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateRecurringVersionRequest,
  RecurringExpenseReportItem,
  RecurringTransaction,
  UpdateRecurringTransactionRequest,
} from '../models/recurring.model';
import { PagedRequest, PagedResult } from '../models/paged-result.model';
import { TransactionType } from '../models/transaction.model';
import { toHttpParams } from './http-params.util';

export interface RecurringFilter extends PagedRequest {
  type?: TransactionType | null;
}

@Injectable({ providedIn: 'root' })
export class RecurringService {
  private readonly baseUrl = `${environment.apiUrl}/recurring-transactions`;

  constructor(private readonly http: HttpClient) {}

  getAll(request: Partial<RecurringFilter> = {}): Observable<PagedResult<RecurringTransaction>> {
    return this.http.get<PagedResult<RecurringTransaction>>(this.baseUrl, {
      params: toHttpParams({ page: 1, pageSize: 20, ...request }),
    });
  }

  /**
   * Relatório imprimível: todos os ativos, sem paginar (o total do rodapé tem de fechar sobre o
   * conjunto inteiro), em shape enxuto.
   */
  getReport(type?: TransactionType): Observable<RecurringExpenseReportItem[]> {
    return this.http.get<RecurringExpenseReportItem[]>(`${this.baseUrl}/report`, {
      params: toHttpParams({ type }),
    });
  }

  getById(id: string): Observable<RecurringTransaction> {
    return this.http.get<RecurringTransaction>(`${this.baseUrl}/${id}`);
  }

  createVersion(id: string, request: CreateRecurringVersionRequest): Observable<RecurringTransaction> {
    return this.http.post<RecurringTransaction>(`${this.baseUrl}/${id}/versions`, request);
  }

  update(id: string, request: UpdateRecurringTransactionRequest): Observable<RecurringTransaction> {
    return this.http.put<RecurringTransaction>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
