import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/paged-result.model';
import {
  CreateTransactionRequest,
  Transaction,
  TransactionFilter,
  UpdateTransactionRequest,
} from '../models/transaction.model';
import { toHttpParams } from './http-params.util';

/** Filtros, ordenação e paginação server-side (regra 19, docs/06-lancamentos.md). */
@Injectable({ providedIn: 'root' })
export class TransactionService {
  private readonly baseUrl = `${environment.apiUrl}/transactions`;

  constructor(private readonly http: HttpClient) {}

  getAll(filter: TransactionFilter): Observable<PagedResult<Transaction>> {
    return this.http.get<PagedResult<Transaction>>(this.baseUrl, { params: toHttpParams(filter as unknown as Record<string, unknown>) });
  }

  getById(id: string): Observable<Transaction> {
    return this.http.get<Transaction>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateTransactionRequest): Observable<Transaction[]> {
    return this.http.post<Transaction[]>(this.baseUrl, request);
  }

  update(id: string, request: UpdateTransactionRequest): Observable<Transaction> {
    return this.http.put<Transaction>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
