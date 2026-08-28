import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MerchantSuggestion } from '../models/merchant.model';
import { toHttpParams } from './http-params.util';

/** RF005 — autocomplete de descrição por prefixo (docs/06-lancamentos.md). */
@Injectable({ providedIn: 'root' })
export class MerchantService {
  private readonly baseUrl = `${environment.apiUrl}/merchants`;

  constructor(private readonly http: HttpClient) {}

  getSuggestions(prefix: string): Observable<MerchantSuggestion[]> {
    const params = toHttpParams({ prefix, maxResults: environment.autocompleteMaxSuggestions });
    return this.http.get<MerchantSuggestion[]>(`${this.baseUrl}/suggestions`, { params });
  }
}
