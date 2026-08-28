import { ChangeDetectorRef, Component, Input, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MatFormFieldModule, MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

/**
 * Input de valor monetário (decimal, nunca float — regra 20). Trabalha com vírgula
 * como separador decimal (padrão brasileiro) e expõe um number puro para o formulário.
 */
@Component({
  selector: 'mf-money-input',
  standalone: true,
  imports: [CommonModule, FormsModule, MatFormFieldModule, MatInputModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => MoneyInputComponent),
      multi: true,
    },
    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { floatLabel: 'always', subscriptSizing: 'dynamic' } },
  ],
  template: `
    <mat-form-field appearance="outline" class="mf-money-input-field">
      <mat-label>{{ label }}</mat-label>
      <span matTextPrefix>R$&nbsp;</span>
      <input
        matInput
        type="text"
        inputmode="decimal"
        [placeholder]="placeholder"
        [(ngModel)]="displayValue"
        (blur)="commit()"
        [disabled]="disabled"
      />
    </mat-form-field>
  `,
  styles: [
    `
      .mf-money-input-field {
        width: 100%;
      }
    `,
  ],
})
export class MoneyInputComponent implements ControlValueAccessor {
  @Input() placeholder = '0,00';
  @Input() label = 'Valor';

  displayValue = '';
  disabled = false;

  private onChange: (value: number | null) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  constructor(private readonly cdr: ChangeDetectorRef) {}

  /**
   * Chamado pela API de forms do Angular (NgModel/ReactiveForms) quando o valor é definido
   * programaticamente pelo componente pai — não é um evento de template deste componente.
   * Sob OnPush real (padrão do Angular 22), é preciso marcar a view manualmente, senão o
   * campo nunca reflete o valor carregado (ex.: ao abrir um lançamento existente para editar).
   */
  writeValue(value: number | null): void {
    this.displayValue = value == null ? '' : value.toFixed(2).replace('.', ',');
    this.cdr.markForCheck();
  }

  registerOnChange(fn: (value: number | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
    this.cdr.markForCheck();
  }

  commit(): void {
    this.onTouched();
    const normalized = this.displayValue.replace(/\./g, '').replace(',', '.').trim();
    const parsed = normalized === '' ? null : Number(normalized);
    this.onChange(Number.isFinite(parsed) ? parsed : null);
    if (parsed !== null) this.writeValue(parsed);
  }
}
