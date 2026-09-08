import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateInvestmentAccountRequest,
  CreateInvestmentRequest,
  CreateInvestmentTransactionRequest,
  Investment,
  InvestmentAccount,
  InvestmentsSummary,
} from '../models/investment.model';
import { toHttpParams } from './http-params.util';

@Injectable({ providedIn: 'root' })
export class InvestmentService {
  private readonly baseUrl = `${environment.apiUrl}/investments`;

  constructor(private readonly http: HttpClient) {}

  getSummary(): Observable<InvestmentsSummary> {
    return this.http.get<InvestmentsSummary>(`${this.baseUrl}/summary`);
  }

  createAccount(request: CreateInvestmentAccountRequest): Observable<InvestmentAccount> {
    return this.http.post<InvestmentAccount>(`${this.baseUrl}/accounts`, request);
  }

  getInvestments(investmentAccountId?: string): Observable<Investment[]> {
    return this.http.get<Investment[]>(this.baseUrl, { params: toHttpParams({ investmentAccountId }) });
  }

  createInvestment(request: CreateInvestmentRequest): Observable<Investment> {
    return this.http.post<Investment>(this.baseUrl, request);
  }

  registerTransaction(request: CreateInvestmentTransactionRequest): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/transactions`, request);
  }
}
