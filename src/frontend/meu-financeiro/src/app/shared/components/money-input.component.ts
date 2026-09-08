import { ChangeDetectorRef, Component, Input, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { InputGroup } from 'primeng/inputgroup';
import { InputGroupAddon } from 'primeng/inputgroupaddon';
import { InputText } from 'primeng/inputtext';

/**
 * Input de valor monetário (decimal, nunca float — regra 20). Trabalha com vírgula
 * como separador decimal (padrão brasileiro) e expõe um number puro para o formulário.
 * Migrado de mat-form-field/matInput pra PrimeNG InputGroup/InputText
 * (docs/migracao-primeng/00-plano.md, UI-01.4) — mesma lógica de formatação/parsing, só a
 * casca do campo mudou. writeValue() continua chamando cdr.markForCheck() (zoneless): o
 * valor pode ser setado programaticamente pelo formulário pai fora de um evento de
 * template, e sem isso o campo nunca reflete o valor carregado.
 */
@Component({
  selector: 'mf-money-input',
  standalone: true,
  imports: [CommonModule, FormsModule, InputGroup, InputGroupAddon, InputText],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => MoneyInputComponent),
      multi: true,
    },
  ],
  template: `
    <label class="mf-money-input-label">{{ label }}</label>
    <p-inputgroup class="mf-money-input-field">
      <p-inputgroup-addon>R$</p-inputgroup-addon>
      <input
        pInputText
        pSize="small"
        type="text"
        inputmode="decimal"
        [placeholder]="placeholder"
        [(ngModel)]="displayValue"
        (blur)="commit()"
        [disabled]="disabled"
      />
    </p-inputgroup>
  `,
  styles: [
    `
      .mf-money-input-label {
        display: block;
        font-size: 0.75rem;
        font-weight: 600;
        color: var(--mf-muted, #8b8175);
        margin-bottom: 0.3rem;
      }
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
