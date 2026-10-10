import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { IssuedCard, IssuedCardRequest } from '../models/issued-card.model';

@Injectable({ providedIn: 'root' })
export class IssuedCardService {
  private readonly baseUrl = environment.apiUrl;

  constructor(private readonly http: HttpClient) {}

  getByGroup(creditCardId: string): Observable<IssuedCard[]> {
    return this.http.get<IssuedCard[]>(`${this.baseUrl}/cards/${creditCardId}/issued-cards`);
  }

  /** Cartões com débito na conta: os só de débito e os múltiplos. */
  getByAccount(accountId: string): Observable<IssuedCard[]> {
    return this.http.get<IssuedCard[]>(`${this.baseUrl}/accounts/${accountId}/debit-cards`);
  }

  /** Cartão só de débito, com bandeira própria. */
  createDebit(accountId: string, request: IssuedCardRequest): Observable<IssuedCard> {
    return this.http.post<IssuedCard>(`${this.baseUrl}/accounts/${accountId}/debit-cards`, request);
  }

  create(creditCardId: string, request: IssuedCardRequest): Observable<IssuedCard> {
    return this.http.post<IssuedCard>(`${this.baseUrl}/cards/${creditCardId}/issued-cards`, request);
  }

  update(id: string, request: IssuedCardRequest): Observable<IssuedCard> {
    return this.http.put<IssuedCard>(`${this.baseUrl}/issued-cards/${id}`, request);
  }

  /** Troca por um cartão novo no mesmo grupo; devolve o novo. */
  replace(id: string, request: IssuedCardRequest): Observable<IssuedCard> {
    return this.http.post<IssuedCard>(`${this.baseUrl}/issued-cards/${id}/replace`, request);
  }

  cancel(id: string): Observable<IssuedCard> {
    return this.http.post<IssuedCard>(`${this.baseUrl}/issued-cards/${id}/cancel`, null);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/issued-cards/${id}`);
  }
}
