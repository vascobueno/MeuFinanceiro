import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateInstallmentPlanRequest,
  InstallmentPlanDetail,
  InstallmentPlanListItem,
  InstallmentPlanResult,
} from '../models/installment-plan.model';
import { PagedRequest, PagedResult } from '../models/paged-result.model';
import { toHttpParams } from './http-params.util';

@Injectable({ providedIn: 'root' })
export class InstallmentPlanService {
  private readonly baseUrl = `${environment.apiUrl}/installment-plans`;

  constructor(private readonly http: HttpClient) {}

  create(request: CreateInstallmentPlanRequest): Observable<InstallmentPlanResult> {
    return this.http.post<InstallmentPlanResult>(this.baseUrl, request);
  }

  getAll(request: Partial<PagedRequest> = {}): Observable<PagedResult<InstallmentPlanListItem>> {
    return this.http.get<PagedResult<InstallmentPlanListItem>>(this.baseUrl, {
      params: toHttpParams({ page: 1, pageSize: 20, ...request }),
    });
  }

  getById(id: string): Observable<InstallmentPlanDetail> {
    return this.http.get<InstallmentPlanDetail>(`${this.baseUrl}/${id}`);
  }
}
