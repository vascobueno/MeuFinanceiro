import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CardInvoice, CardInvoiceFilter } from '../models/invoice.model';
import { PagedResult } from '../models/paged-result.model';
import { toHttpParams } from './http-params.util';

@Injectable({ providedIn: 'root' })
export class InvoiceService {
  private readonly baseUrl = `${environment.apiUrl}/card-invoices`;

  constructor(private readonly http: HttpClient) {}

  /** Exclui uma fatura importada por engano (o servidor bloqueia se houver conciliação aceita). */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  getAll(filter: Partial<CardInvoiceFilter> = {}): Observable<PagedResult<CardInvoice>> {
    const params = toHttpParams({ page: 1, pageSize: 20, ...filter });
    return this.http.get<PagedResult<CardInvoice>>(this.baseUrl, { params });
  }
}
