import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Select } from 'primeng/select';
import { Category } from '../../core/models/category.model';
import { TransactionFilter, TransactionStatus, TransactionType } from '../../core/models/transaction.model';
import { DateRange, DateRangeComponent } from './date-range.component';

/**
 * Card de filtros das telas de Lançamentos.
 *
 * Sem os selects de Conta e Cartão: as duas telas já escolhem um no topo, então repeti-los aqui
 * seria dois controles para a mesma coisa.
 */
@Component({
  selector: 'mf-transaction-filters',
  standalone: true,
  imports: [CommonModule, FormsModule, Select, DateRangeComponent],
  template: `
    <div class="mf-filters-card">
      <button
        type="button"
        class="mf-filters-toggle"
        [class.mf-filters-toggle-open]="expanded()"
        (click)="expanded.set(!expanded())"
        [attr.aria-expanded]="expanded()"
      >
        <span>Filtros</span>
        <i class="pi" [ngClass]="expanded() ? 'pi-chevron-up' : 'pi-chevron-down'"></i>
      </button>

      @if (expanded()) {
        <div class="mf-filters-grid">
          <mf-date-range
            class="mf-filter-field mf-filter-field-period"
            size="small"
            [range]="dateRange"
            (rangeChange)="onDateRangeChange($event)"
          ></mf-date-range>

          <div class="mf-filter-field">
            <label class="mf-field-label">Categoria</label>
            <p-select
              size="small"
              class="mf-field-input"
              [options]="categoryOptions"
              optionLabel="name"
              optionValue="id"
              [(ngModel)]="filter.categoryId"
              (onChange)="changed.emit()"
            />
          </div>

          <!-- Só na tela do cartão, com os cartões do grupo: titular, adicionais e virtuais. -->
          @if (cardOptions.length > 0) {
            <div class="mf-filter-field">
              <label class="mf-field-label">Cartão</label>
              <p-select
                size="small"
                class="mf-field-input"
                [options]="cardFilterOptions"
                optionLabel="label"
                optionValue="value"
                [(ngModel)]="filter.issuedCardId"
                (onChange)="changed.emit()"
              />
            </div>
          }

          <div class="mf-filter-field mf-filter-field-tipo">
            <label class="mf-field-label">Tipo</label>
            <p-select
              size="small"
              class="mf-field-input"
              [options]="typeOptions"
              optionLabel="label"
              optionValue="value"
              [(ngModel)]="filter.type"
              (onChange)="changed.emit()"
            />
          </div>

          <div class="mf-filter-field mf-filter-field-status">
            <label class="mf-field-label">Status</label>
            <p-select
              size="small"
              class="mf-field-input"
              [options]="statusOptions"
              optionLabel="label"
              optionValue="value"
              [(ngModel)]="filter.status"
              (onChange)="changed.emit()"
            />
          </div>
        </div>
      }
    </div>
  `,
  styles: [
    `
.mf-filters-card {
        background: var(--mf-card);
        border: 1px solid var(--mf-line);
        border-radius: var(--mf-radius);
        /* Metade da altura anterior: a caixa fechada é só o botão de abrir, e 1rem de padding
           em cima e embaixo dobrava a altura dela sem informação nenhuma. */
        padding: 0.3rem 0.75rem;
        margin-bottom: 1rem;
      }
      .mf-filters-toggle {
        display: flex;
        align-items: center;
        justify-content: space-between;
        width: 100%;
        border: none;
        background: none;
        padding: 0;
        margin: 0;
        font: inherit;
        font-weight: 600;
        line-height: 1.2;
        color: var(--mf-text);
        cursor: pointer;
      }
      .mf-filters-toggle .pi {
        font-size: 0.85rem;
        color: var(--mf-muted);
      }
      .mf-filters-toggle.mf-filters-toggle-open {
        margin-bottom: 0.35rem;
      }
      .mf-filters-grid {
        display: flex;
        flex-wrap: wrap;
        gap: 0.75rem;
        padding-top: 0.25rem;
      }
      .mf-filter-field {
        flex: 1 1 180px;
      }
      /* Larguras fixas para o campo conter o conteúdo, sem esticar: o período precisa das duas
         datas, e os dois selects, do maior rótulo. */
      .mf-filter-field-period {
        flex: 0 0 245px;
      }
      .mf-filter-field-tipo {
        flex: 0 0 160px;
      }
      .mf-filter-field-status {
        flex: 0 0 140px;
      }
      @media (max-width: 640px) {
        .mf-filter-field,
        .mf-filter-field-period,
        .mf-filter-field-tipo,
        .mf-filter-field-status {
          flex: 1 1 100%;
        }
      }
    `,
  ],
})
export class TransactionFiltersComponent {
  /** Objeto mutado diretamente, como as telas já faziam — o pai relê depois de `changed`. */
  @Input({ required: true }) filter!: TransactionFilter;
  @Input() categories: Category[] = [];
  /** Os cartões do grupo; vazio esconde o filtro (tela da conta, grupo sem cartão cadastrado). */
  @Input() cardOptions: Array<{ label: string; value: string }> = [];

  @Output() changed = new EventEmitter<void>();

  /** Começa fechado, como as telas de lista do app. */
  readonly expanded = signal(false);

  dateRange: DateRange = { start: null, end: null };

  readonly typeOptions = [
    { label: 'Todos', value: null },
    { label: 'Receita', value: TransactionType.Income },
    { label: 'Despesa', value: TransactionType.Expense },
    { label: 'Transferência', value: TransactionType.Transfer },
    { label: 'Pagamento de cartão', value: TransactionType.CardPayment },
  ];

  readonly statusOptions = [
    { label: 'Todos', value: null },
    { label: 'Pendente', value: TransactionStatus.Pending },
    { label: 'Confirmado', value: TransactionStatus.Confirmed },
    { label: 'Conciliado', value: TransactionStatus.Reconciled },
  ];

  /**
   * Getter, e não computed(): `categories` é @Input comum, não signal — um computed não
   * recalcularia quando a lista chegasse do serviço.
   */
  get categoryOptions(): Array<{ id: string | null; name: string }> {
    return [{ id: null, name: 'Todas' }, ...this.categories];
  }

  get cardFilterOptions(): Array<{ label: string; value: string | null }> {
    return [{ label: 'Todos', value: null }, ...this.cardOptions];
  }

  onDateRangeChange(range: DateRange): void {
    this.dateRange = range;
    this.filter.startDate = range.start;
    this.filter.endDate = range.end;
    this.changed.emit();
  }
}
