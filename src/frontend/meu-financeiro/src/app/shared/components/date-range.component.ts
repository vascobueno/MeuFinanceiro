import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DatePicker } from 'primeng/datepicker';

export interface DateRange {
  start: string | null;
  end: string | null;
}

/**
 * Filtro de período reutilizável (docs/06-lancamentos.md — filtro por período). Migrado de
 * mat-date-range-input pra PrimeNG DatePicker[selectionMode="range"]
 * (docs/migracao-primeng/00-plano.md, UI-01.5 — ticket de maior risco da Fase 1).
 *
 * Diferente do Material (dois inputs independentes matStartDate/matEndDate), o DatePicker
 * em modo "range" é um único widget com estado interno multi-clique (primeiro clique marca
 * o início, segundo clique completa o range). `rangeDates` é um campo comum (não um getter
 * que recria Date a cada leitura), sincronizado a partir do `@Input() range` só quando o
 * valor externo realmente muda — nunca durante a própria seleção do usuário no calendário.
 *
 * Achado confirmado via QA visual (Playwright): representar "nenhum range ainda" como
 * `[null, null]` faz o próprio PrimeNG quebrar no primeiro clique
 * (`Cannot read properties of null (reading 'getTime')`) — o código interno do DatePicker
 * decide "já existe uma seleção em andamento?" checando só `this.value.length > 0`, sem
 * checar se os elementos são null, então `[null, null]` (length 2) engana essa checagem e
 * ele tenta `startDate.getTime()` num `null`. Por isso "sem range" é representado aqui como
 * array vazio `[]` (length 0, cai corretamente no branch de "começar um range novo"); só
 * depois que existe um início real o array vira `[startDate, endDate | null]`.
 */
@Component({
  selector: 'mf-date-range',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePicker],
  template: `
    <label class="mf-date-range-label">Período</label>
    <p-datepicker
      class="mf-date-range-field"
      selectionMode="range"
      dateFormat="dd/mm/yy"
      [showIcon]="true"
      iconDisplay="input"
      [showButtonBar]="true"
      [size]="size"
      placeholder="Início — Fim"
      [ngModel]="rangeDates"
      (ngModelChange)="onRangeChange($event)"
    />
  `,
  styles: [
    `
      .mf-date-range-label {
        display: block;
        font-size: 0.75rem;
        font-weight: 600;
        color: var(--mf-muted, #8b8175);
        margin-bottom: 0.3rem;
      }
      .mf-date-range-field {
        display: block;
        width: 100%;
      }
      .mf-date-range-field ::ng-deep .p-datepicker-input {
        width: 100%;
      }
    `,
  ],
})
export class DateRangeComponent implements OnChanges {
  @Input() range: DateRange = { start: null, end: null };
  @Input() size?: 'small' | 'large';
  @Output() rangeChange = new EventEmitter<DateRange>();

  rangeDates: (Date | null)[] = [];

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['range']) return;
    // Só resincroniza a partir do @Input() quando o valor externo (ISO) realmente diverge
    // do que rangeDates já representa — evita recriar Date novos (e reescrever no
    // DatePicker) a cada eco de volta do próprio (ngModelChange) desta interação.
    if (this.toIsoDate(this.rangeDates[0] ?? null) !== this.range.start || this.toIsoDate(this.rangeDates[1] ?? null) !== this.range.end) {
      this.rangeDates = this.toModelArray(this.fromIsoDate(this.range.start), this.fromIsoDate(this.range.end));
    }
  }

  // "Sem início" tem que ser array vazio, não [null, null] — ver comentário da classe.
  private toModelArray(start: Date | null, end: Date | null): (Date | null)[] {
    return start ? [start, end] : [];
  }

  // new Date('yyyy-MM-dd') interpreta a string como UTC (spec ES2015), então em fusos
  // negativos (ex.: America/Sao_Paulo) o dia local vira o dia anterior — por isso o parse
  // manual dos componentes, que sempre constrói a data em horário local.
  private fromIsoDate(value: string | null | undefined): Date | null {
    if (!value) return null;
    const [year, month, day] = value.split('-').map(Number);
    return new Date(year, month - 1, day);
  }

  onRangeChange(value: (Date | null)[] | null): void {
    const [start, end] = value ?? [];
    this.rangeDates = this.toModelArray(start ?? null, end ?? null);
    this.range = { start: this.toIsoDate(start), end: this.toIsoDate(end) };
    this.rangeChange.emit(this.range);
  }

  private toIsoDate(value: Date | null | undefined): string | null {
    if (!value) return null;
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
