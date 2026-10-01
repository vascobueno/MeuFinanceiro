import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CardInvoiceCycle,
  CardPayment,
  CreateCardPaymentRequest,
  UpdateCardPaymentRequest,
} from '../models/card-payment.model';

/**
 * Fatura calculada do ciclo e seus pagamentos (docs/05 — Pagamento de cartão).
 *
 * A fatura é sub-recurso do cartão (`/cards/{id}/invoice`) porque é derivada dos lançamentos, não
 * armazenada — diferente de `InvoiceService`, que lista os PDFs importados do banco.
 */
@Injectable({ providedIn: 'root' })
export class CardPaymentService {
  private readonly cardsUrl = `${environment.apiUrl}/cards`;
  private readonly paymentsUrl = `${environment.apiUrl}/card-payments`;

  constructor(private readonly http: HttpClient) {}

  /** `reference` é a data de fechamento do ciclo; omitida, devolve o ciclo atual. */
  getCycle(cardId: string, reference?: string | null): Observable<CardInvoiceCycle> {
    const params = reference ? new HttpParams().set('reference', reference) : undefined;
    return this.http.get<CardInvoiceCycle>(`${this.cardsUrl}/${cardId}/invoice`, { params });
  }

  /** Ajusta o fechamento de UMA fatura — quando a operadora antecipa ou adia. */
  setClosing(cardId: string, reference: string, closingDate: string): Observable<CardInvoiceCycle> {
    return this.http.put<CardInvoiceCycle>(
      `${this.cardsUrl}/${cardId}/invoice/${reference}/closing`, { closingDate });
  }

  /** Remove o ajuste e volta ao dia de fechamento do cadastro. */
  clearClosing(cardId: string, reference: string): Observable<CardInvoiceCycle> {
    return this.http.delete<CardInvoiceCycle>(
      `${this.cardsUrl}/${cardId}/invoice/${reference}/closing`);
  }

  create(request: CreateCardPaymentRequest): Observable<CardPayment> {
    return this.http.post<CardPayment>(this.paymentsUrl, request);
  }

  update(id: string, request: UpdateCardPaymentRequest): Observable<CardPayment> {
    return this.http.put<CardPayment>(`${this.paymentsUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.paymentsUrl}/${id}`);
  }
}
