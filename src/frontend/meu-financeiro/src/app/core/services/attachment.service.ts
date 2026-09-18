import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Attachment, AttachmentOwner } from '../models/attachment.model';

/** Anexos de lançamento e de parcelamento (docs/06, docs/07). */
@Injectable({ providedIn: 'root' })
export class AttachmentService {
  private readonly baseUrl = `${environment.apiUrl}/attachments`;

  constructor(private readonly http: HttpClient) {}

  list(owner: AttachmentOwner): Observable<Attachment[]> {
    return this.http.get<Attachment[]>(this.baseUrl, { params: this.ownerParams(owner) });
  }

  upload(owner: AttachmentOwner, file: File): Observable<Attachment> {
    const formData = new FormData();
    formData.append('file', file);
    // Sem Content-Type explícito: o navegador precisa definir o boundary do multipart.
    return this.http.post<Attachment>(this.baseUrl, formData, { params: this.ownerParams(owner) });
  }

  delete(attachmentId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${attachmentId}`);
  }

  /**
   * Baixa como blob em vez de apontar um link direto para a URL: o endpoint exige o header de
   * autenticação que o interceptor injeta, e um &lt;a href&gt; comum não passaria por ele.
   */
  download(attachmentId: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${attachmentId}/content`, { responseType: 'blob' });
  }

  private ownerParams(owner: AttachmentOwner): HttpParams {
    const key = owner.kind === 'transaction' ? 'transactionId' : 'installmentPlanId';
    return new HttpParams().set(key, owner.id);
  }
}
