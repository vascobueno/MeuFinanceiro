import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateInstallmentPlanRequest, InstallmentPlanResult } from '../models/installment-plan.model';

@Injectable({ providedIn: 'root' })
export class InstallmentPlanService {
  private readonly baseUrl = `${environment.apiUrl}/installment-plans`;

  constructor(private readonly http: HttpClient) {}

  create(request: CreateInstallmentPlanRequest): Observable<InstallmentPlanResult> {
    return this.http.post<InstallmentPlanResult>(this.baseUrl, request);
  }
}
