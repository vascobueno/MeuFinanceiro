import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateRecurringVersionRequest, RecurringTransaction, UpdateRecurringTransactionRequest } from '../models/recurring.model';
import { TransactionType } from '../models/transaction.model';
import { toHttpParams } from './http-params.util';

@Injectable({ providedIn: 'root' })
export class RecurringService {
  private readonly baseUrl = `${environment.apiUrl}/recurring-transactions`;

  constructor(private readonly http: HttpClient) {}

  /** type opcional — usado pelo relatório "Gastos recorrentes" (type=Expense). */
  getAll(type?: TransactionType): Observable<RecurringTransaction[]> {
    return this.http.get<RecurringTransaction[]>(this.baseUrl, { params: toHttpParams({ type }) });
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
