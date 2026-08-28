import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Account, CreateAccountRequest, UpdateAccountRequest } from '../models/account.model';
import { PagedRequest, PagedResult } from '../models/paged-result.model';
import { toHttpParams } from './http-params.util';

@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly baseUrl = `${environment.apiUrl}/accounts`;

  constructor(private readonly http: HttpClient) {}

  getAll(request: Partial<PagedRequest> = {}): Observable<PagedResult<Account>> {
    const params: HttpParams = toHttpParams({ page: 1, pageSize: 20, ...request });
    return this.http.get<PagedResult<Account>>(this.baseUrl, { params });
  }

  getById(id: string): Observable<Account> {
    return this.http.get<Account>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateAccountRequest): Observable<Account> {
    return this.http.post<Account>(this.baseUrl, request);
  }

  update(id: string, request: UpdateAccountRequest): Observable<Account> {
    return this.http.put<Account>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
