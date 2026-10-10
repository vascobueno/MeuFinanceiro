import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BankStatement, BankStatementFilter } from '../models/statement.model';
import { PagedResult } from '../models/paged-result.model';
import { toHttpParams } from './http-params.util';

@Injectable({ providedIn: 'root' })
export class StatementService {
  private readonly baseUrl = `${environment.apiUrl}/bank-statements`;

  constructor(private readonly http: HttpClient) {}

  /** Exclui um extrato importado por engano (o servidor bloqueia se houver conciliação aceita). */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  getAll(filter: Partial<BankStatementFilter> = {}): Observable<PagedResult<BankStatement>> {
    const params = toHttpParams({ page: 1, pageSize: 20, ...filter });
    return this.http.get<PagedResult<BankStatement>>(this.baseUrl, { params });
  }
}
