import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Projection } from '../models/projection.model';
import { toHttpParams } from './http-params.util';

@Injectable({ providedIn: 'root' })
export class ProjectionService {
  private readonly baseUrl = `${environment.apiUrl}/projection`;

  constructor(private readonly http: HttpClient) {}

  getProjection(date: string): Observable<Projection> {
    return this.http.get<Projection>(this.baseUrl, { params: toHttpParams({ date }) });
  }
}
