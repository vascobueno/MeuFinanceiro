import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuditLogEntry, AuditLogFilter } from '../models/audit.model';
import { PagedResult } from '../models/paged-result.model';
import { toHttpParams } from './http-params.util';

@Injectable({ providedIn: 'root' })
export class AuditService {
  private readonly baseUrl = `${environment.apiUrl}/audit`;

  constructor(private readonly http: HttpClient) {}

  getAll(filter: AuditLogFilter): Observable<PagedResult<AuditLogEntry>> {
    return this.http.get<PagedResult<AuditLogEntry>>(this.baseUrl, { params: toHttpParams({ ...filter }) });
  }
}
