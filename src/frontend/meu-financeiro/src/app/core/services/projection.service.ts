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

  /**
   * maxTimelineEntries limita quantas entradas voltam na resposta; os agregados
   * (timelineCount, projectedExpensesTotal) continuam sobre a linha completa. Use quando só
   * precisar dos números e de uma prévia — evita trafegar a linha do tempo inteira.
   */
  getProjection(date: string, maxTimelineEntries?: number): Observable<Projection> {
    return this.http.get<Projection>(this.baseUrl, { params: toHttpParams({ date, maxTimelineEntries }) });
  }
}
