import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Table, SortableColumn, SortIcon } from 'primeng/table';
import { Tooltip } from 'primeng/tooltip';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { Transaction } from '../../core/models/transaction.model';
import { ListStateComponent } from './list-state.component';
import { StatusBadgeComponent } from './status-badge.component';
import { formatForeign } from '../../core/services/foreign.util';

/**
 * Tabela de lançamentos, compartilhada pelas telas de conta e de cartão.
 *
 * Não tem a coluna "Origem": nas duas telas ela seria a mesma informação repetida em todas as
 * linhas, já que a tela inteira é de uma conta ou de um cartão.
 *
 * O diálogo de confirmação vive aqui, e não nas telas: a exclusão é ação de linha, e deixá-la no
 * componente evita repetir o mesmo ConfirmationService nas duas.
 */
@Component({
  selector: 'mf-transaction-table',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    Table,
    SortableColumn,
    SortIcon,
    Tooltip,
    ConfirmDialog,
    ListStateComponent,
    StatusBadgeComponent,
  ],
  providers: [ConfirmationService],
  template: `
    <p-confirm-dialog />

    <mf-list-state
      [loading]="loading"
      [error]="error"
      [empty]="!loading && !error && transactions.length === 0"
      [emptyMessage]="emptyMessage"
      [onRetry]="onRetry"
    ></mf-list-state>

    @if (!loading && !error && transactions.length > 0) {
      <!-- Sem [sortField]/[sortOrder]: o p-table sincroniza esses dois inputs via um effect()
           interno que chama sortSingle() (reemite onSort) sempre que o valor muda. Alimentá-los
           a partir de sortBy/sortDir — que o próprio (onSort) também escreve — fecha um ciclo e
           vira loop infinito de requisições. Sem os inputs, onSort dispara UMA vez por clique,
           que é tudo que o load() precisa. Mesmo padrão nas demais listas do app. -->
      <p-table [value]="transactions" [customSort]="true" (onSort)="sort.emit($event)" size="small">
        <ng-template #header>
          <tr>
            <th pSortableColumn="transactionDate">Data <p-sort-icon field="transactionDate" /></th>
            <th pSortableColumn="description">Descrição <p-sort-icon field="description" /></th>
            <th>Categoria</th>
            <th pSortableColumn="amount" class="mf-col-end">Valor <p-sort-icon field="amount" /></th>
            <th pSortableColumn="status">Status <p-sort-icon field="status" /></th>
            <th></th>
          </tr>
        </ng-template>

        <ng-template #body let-transaction>
          <tr [routerLink]="['/lancamentos', transaction.id]" class="mf-clickable-row">
            <td>{{ transaction.transactionDate | date: 'dd/MM/yyyy' }}</td>
            <td>
              <!-- O "(n/total)" da parcela já vem gravado na descrição pelo backend; não
                   concatenar aqui, senão duplica. -->
              {{ transaction.description }}
              @if (transaction.foreign) {
                <div class="mf-sub">{{ foreignLine(transaction) }}</div>
              }
              @if (transaction.notes) {
                <span
                  class="mf-sub"
                  style="cursor: help"
                  [title]="transaction.notes"
                  (click)="$event.stopPropagation()"
                  role="img"
                  aria-label="Observação"
                >
                  📝
                </span>
              }
              @if (transaction.attachmentCount) {
                <span
                  class="mf-sub mf-attachment-badge"
                  [title]="transaction.attachmentCount === 1 ? '1 anexo' : transaction.attachmentCount + ' anexos'"
                  (click)="$event.stopPropagation()"
                  role="img"
                  [attr.aria-label]="transaction.attachmentCount + ' anexo(s)'"
                >
                  <i class="pi pi-paperclip"></i>
                  @if (transaction.attachmentCount > 1) {
                    {{ transaction.attachmentCount }}
                  }
                </span>
              }
            </td>
            <td>{{ transaction.categoryName || '—' }}</td>
            <td class="mf-col-end">{{ transaction.amount | currency: 'BRL' }}</td>
            <td><mf-status-badge [status]="transaction.status"></mf-status-badge></td>
            <td class="mf-col-end">
              @if (canWrite) {
                @if (transaction.recurringTransactionId) {
                  <a
                    class="mf-row-edit-icon"
                    pTooltip="Editar recorrência"
                    tooltipPosition="top"
                    [attr.aria-label]="'Editar recorrência'"
                    [routerLink]="['/recorrencia', transaction.recurringTransactionId]"
                    (click)="$event.stopPropagation()"
                  >
                    <i class="pi pi-pencil"></i>
                  </a>
                }
                <button
                  type="button"
                  class="mf-row-delete-icon"
                  pTooltip="Excluir"
                  tooltipPosition="top"
                  [attr.aria-label]="'Excluir'"
                  (click)="$event.stopPropagation(); confirmDelete(transaction)"
                >
                  <i class="pi pi-trash"></i>
                </button>
              }
            </td>
          </tr>
        </ng-template>
      </p-table>
    }
  `,
  styles: [
    `
.mf-clickable-row {
        cursor: pointer;
      }
      .mf-col-end {
        text-align: right;
      }
      .mf-attachment-badge {
        display: inline-flex;
        align-items: center;
        gap: 0.15rem;
        margin-left: 0.4rem;
        cursor: help;
      }
      .mf-row-edit-icon,
      .mf-row-delete-icon {
        background: none;
        border: none;
        padding: 0;
        cursor: pointer;
        font-size: 1rem;
      }
      .mf-row-edit-icon {
        color: var(--mf-primary, #e2572b);
        margin-right: 0.75rem;
      }
      .mf-row-delete-icon {
        color: var(--mf-red, #c0392b);
      }
    `,
  ],
})
export class TransactionTableComponent {
  private readonly confirmationService = inject(ConfirmationService);

  @Input() transactions: Transaction[] = [];
  @Input() loading = false;
  @Input() error: string | null = null;
  @Input() canWrite = false;
  @Input() emptyMessage = 'Nenhum lançamento encontrado para os filtros selecionados.';
  @Input() onRetry?: () => void;

  @Output() sort = new EventEmitter<{ field?: string; order?: number }>();
  /** Emitido só depois da confirmação. */
  @Output() remove = new EventEmitter<Transaction>();

  /** "US$ 21,22 × 5,4970" sob a descrição da compra no exterior. */
  foreignLine(transaction: Transaction): string {
    return formatForeign(transaction.foreign);
  }

  confirmDelete(transaction: Transaction): void {
    this.confirmationService.confirm({
      header: 'Excluir lançamento',
      message: `Excluir "${transaction.description}"? Esta ação não pode ser desfeita.`,
      acceptLabel: 'Excluir',
      rejectLabel: 'Cancelar',
      accept: () => this.remove.emit(transaction),
    });
  }
}
