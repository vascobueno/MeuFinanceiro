import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ParseBankStatementRequest,
  ParsedBankStatement,
  ParsedOfx,
  ParseCardInvoiceRequest,
  ParsedCardInvoice,
  BankReconciliationItem,
  BankStatement,
  CardInvoice,
  CardReconciliationItem,
  ImportBankStatementRequest,
  ImportCardInvoiceRequest,
} from '../models/reconciliation.model';
import { toHttpParams } from './http-params.util';

/** docs/09-conciliacao-conta-corrente.md e docs/08-conciliacao-cartoes.md. */
@Injectable({ providedIn: 'root' })
export class ReconciliationService {
  private readonly baseUrl = environment.apiUrl;

  constructor(private readonly http: HttpClient) {}

  /** O servidor lê o texto colado do extrato — não grava nada. */
  parseBankStatement(request: ParseBankStatementRequest): Observable<ParsedBankStatement> {
    return this.http.post<ParsedBankStatement>(`${this.baseUrl}/bank-statements/parse`, request);
  }

  /** O servidor lê o OFX e reconhece a conta — não grava nada. */
  parseOfx(file: File): Observable<ParsedOfx> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<ParsedOfx>(`${this.baseUrl}/bank-statements/parse-ofx`, form);
  }

  /** O servidor lê o texto colado da fatura — não grava nada. */
  parseCardInvoice(request: ParseCardInvoiceRequest): Observable<ParsedCardInvoice> {
    return this.http.post<ParsedCardInvoice>(`${this.baseUrl}/card-invoices/parse`, request);
  }

  importBankStatement(request: ImportBankStatementRequest): Observable<BankStatement> {
    return this.http.post<BankStatement>(`${this.baseUrl}/bank-statements/import`, request);
  }

  getBankReconciliations(accountId?: string, status?: string): Observable<BankReconciliationItem[]> {
    return this.http.get<BankReconciliationItem[]>(`${this.baseUrl}/reconciliations/bank`, {
      params: toHttpParams({ accountId, status }),
    });
  }

  acceptBankReconciliation(id: string): Observable<BankReconciliationItem> {
    return this.http.post<BankReconciliationItem>(`${this.baseUrl}/reconciliations/bank/${id}/accept`, {});
  }

  linkBankReconciliationManually(bankStatementItemId: string, financialTransactionId: string): Observable<BankReconciliationItem> {
    return this.http.post<BankReconciliationItem>(`${this.baseUrl}/reconciliations/bank/manual`, {
      bankStatementItemId,
      financialTransactionId,
    });
  }

  undoBankReconciliation(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/reconciliations/bank/${id}/undo`, {});
  }

  importCardInvoice(request: ImportCardInvoiceRequest): Observable<CardInvoice> {
    return this.http.post<CardInvoice>(`${this.baseUrl}/card-invoices/import`, request);
  }

  getCardReconciliations(creditCardId?: string, status?: string): Observable<CardReconciliationItem[]> {
    return this.http.get<CardReconciliationItem[]>(`${this.baseUrl}/reconciliations/card`, {
      params: toHttpParams({ creditCardId, status }),
    });
  }

  /** "Lançar": cria no cartão o lançamento de um item sem par e já o concilia. */
  createTransactionFromCardItem(reconciliationId: string, categoryId?: string | null): Observable<CardReconciliationItem> {
    return this.http.post<CardReconciliationItem>(
      `${this.baseUrl}/reconciliations/card/${reconciliationId}/create-transaction`,
      { categoryId: categoryId ?? null },
    );
  }

  acceptCardReconciliation(id: string): Observable<CardReconciliationItem> {
    return this.http.post<CardReconciliationItem>(`${this.baseUrl}/reconciliations/card/${id}/accept`, {});
  }

  linkCardReconciliationManually(cardInvoiceItemId: string, financialTransactionId: string): Observable<CardReconciliationItem> {
    return this.http.post<CardReconciliationItem>(`${this.baseUrl}/reconciliations/card/manual`, {
      cardInvoiceItemId,
      financialTransactionId,
    });
  }

  undoCardReconciliation(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/reconciliations/card/${id}/undo`, {});
  }
}
