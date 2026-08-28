import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateCardRequest, CreditCard, UpdateCardRequest } from '../models/card.model';
import { PagedRequest, PagedResult } from '../models/paged-result.model';
import { toHttpParams } from './http-params.util';

@Injectable({ providedIn: 'root' })
export class CardService {
  private readonly baseUrl = `${environment.apiUrl}/cards`;

  constructor(private readonly http: HttpClient) {}

  getAll(request: Partial<PagedRequest> = {}): Observable<PagedResult<CreditCard>> {
    return this.http.get<PagedResult<CreditCard>>(this.baseUrl, { params: toHttpParams({ page: 1, pageSize: 20, ...request }) });
  }

  getById(id: string): Observable<CreditCard> {
    return this.http.get<CreditCard>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateCardRequest): Observable<CreditCard> {
    return this.http.post<CreditCard>(this.baseUrl, request);
  }

  update(id: string, request: UpdateCardRequest): Observable<CreditCard> {
    return this.http.put<CreditCard>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
