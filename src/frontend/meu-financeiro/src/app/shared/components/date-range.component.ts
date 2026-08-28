import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule, MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';

export interface DateRange {
  start: string | null;
  end: string | null;
}

/** Filtro de período reutilizável (docs/06-lancamentos.md — filtro por período), sobre mat-date-range-input. */
@Component({
  selector: 'mf-date-range',
  standalone: true,
  imports: [CommonModule, FormsModule, MatFormFieldModule, MatDatepickerModule],
  providers: [
    provideNativeDateAdapter(),
    { provide: MAT_DATE_LOCALE, useValue: 'pt-BR' },
    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { floatLabel: 'always', subscriptSizing: 'dynamic' } },
  ],
  template: `
    <mat-form-field appearance="outline" class="mf-date-range-field">
      <mat-label>Período</mat-label>
      <mat-date-range-input [rangePicker]="picker">
        <input matStartDate placeholder="Início" [ngModel]="startDate" (ngModelChange)="onStartChange($event)" />
        <input matEndDate placeholder="Fim" [ngModel]="endDate" (ngModelChange)="onEndChange($event)" />
      </mat-date-range-input>
      <mat-datepicker-toggle matIconSuffix [for]="picker"></mat-datepicker-toggle>
      <mat-date-range-picker #picker></mat-date-range-picker>
    </mat-form-field>
  `,
  styles: [
    `
      .mf-date-range-field {
        width: 100%;
      }
    `,
  ],
})
export class DateRangeComponent {
  @Input() range: DateRange = { start: null, end: null };
  @Output() rangeChange = new EventEmitter<DateRange>();

  // Caches a última conversão string->Date por campo: um getter que devolvesse um `new Date(...)"
  // novo a cada leitura quebraria a igualdade referencial que o matDatepicker usa para decidir
  // se o valor mudou, disparando escrita -> markForCheck -> nova leitura -> loop infinito de
  // change detection (travou a aba nos testes deste componente sob Angular zoneless).
  private startCache: { iso: string | null; date: Date | null } = { iso: undefined as never, date: null };
  private endCache: { iso: string | null; date: Date | null } = { iso: undefined as never, date: null };

  get startDate(): Date | null {
    if (this.startCache.iso !== this.range.start) {
      this.startCache = { iso: this.range.start, date: this.fromIsoDate(this.range.start) };
    }
    return this.startCache.date;
  }

  get endDate(): Date | null {
    if (this.endCache.iso !== this.range.end) {
      this.endCache = { iso: this.range.end, date: this.fromIsoDate(this.range.end) };
    }
    return this.endCache.date;
  }

  // new Date('yyyy-MM-dd') interpreta a string como UTC (spec ES2015), então em fusos
  // negativos (ex.: America/Sao_Paulo) o dia local vira o dia anterior — por isso o parse
  // manual dos componentes, que sempre constrói a data em horário local.
  private fromIsoDate(value: string | null | undefined): Date | null {
    if (!value) return null;
    const [year, month, day] = value.split('-').map(Number);
    return new Date(year, month - 1, day);
  }

  onStartChange(value: Date | null): void {
    this.range = { ...this.range, start: this.toIsoDate(value) };
    this.rangeChange.emit(this.range);
  }

  onEndChange(value: Date | null): void {
    this.range = { ...this.range, end: this.toIsoDate(value) };
    this.rangeChange.emit(this.range);
  }

  private toIsoDate(value: Date | null): string | null {
    if (!value) return null;
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
