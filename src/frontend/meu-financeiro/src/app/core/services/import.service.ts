import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ConfirmImportResult, ImportFileHistoryItem, ImportPreview } from '../models/import.model';

/** docs/16-importacao-inicial.md: Excel -> Upload -> Validação -> Prévia -> Importação -> Relatório. */
@Injectable({ providedIn: 'root' })
export class ImportService {
  private readonly baseUrl = `${environment.apiUrl}/imports`;

  constructor(private readonly http: HttpClient) {}

  getHistory(): Observable<ImportFileHistoryItem[]> {
    return this.http.get<ImportFileHistoryItem[]>(this.baseUrl);
  }

  uploadAndValidate(file: File, force = false): Observable<ImportPreview> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<ImportPreview>(`${this.baseUrl}/excel?force=${force}`, formData);
  }

  confirm(importFileId: string): Observable<ConfirmImportResult> {
    return this.http.post<ConfirmImportResult>(`${this.baseUrl}/excel/${importFileId}/confirm`, {});
  }
}
