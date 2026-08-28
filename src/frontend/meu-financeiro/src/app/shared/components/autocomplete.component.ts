import { ChangeDetectorRef, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule, MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { Subject, debounceTime, distinctUntilChanged, switchMap, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MerchantSuggestion } from '../../core/models/merchant.model';
import { MerchantService } from '../../core/services/merchant.service';

/**
 * Autocomplete de descrição por prefixo (RF005, regra 18). Debounce ~300ms e no máximo
 * 10-15 sugestões (docs/06-lancamentos.md, .github/copilot-instructions.md).
 */
@Component({
  selector: 'mf-description-autocomplete',
  standalone: true,
  imports: [CommonModule, FormsModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule],
  providers: [
    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { floatLabel: 'always', subscriptSizing: 'dynamic' } },
  ],
  template: `
    <mat-form-field appearance="outline" class="mf-autocomplete-field">
      <mat-label>Descrição</mat-label>
      <input
        matInput
        placeholder="Ex.: Amazon"
        [(ngModel)]="value"
        (ngModelChange)="onInputChange($event)"
        [matAutocomplete]="auto"
      />
      <mat-autocomplete #auto="matAutocomplete" (optionSelected)="onOptionSelected($event)">
        @for (suggestion of suggestions; track suggestion.name) {
          <mat-option [value]="suggestion.name">{{ suggestion.name }}</mat-option>
        }
      </mat-autocomplete>
    </mat-form-field>
  `,
  styles: [
    `
      .mf-autocomplete-field {
        width: 100%;
      }
    `,
  ],
})
export class AutocompleteComponent {
  @Input() value = '';
  @Output() valueChange = new EventEmitter<string>();
  @Output() suggestionSelected = new EventEmitter<MerchantSuggestion>();

  suggestions: MerchantSuggestion[] = [];

  private readonly input$ = new Subject<string>();

  constructor(
    private readonly merchantService: MerchantService,
    private readonly cdr: ChangeDetectorRef,
  ) {
    // Emissão do RxJS chega em uma macrotask própria (debounce + XHR), fora de qualquer
    // evento de template desta view — sob OnPush real (padrão do Angular 22), é preciso
    // marcar a view manualmente ou a lista de sugestões nunca aparece na tela.
    this.input$
      .pipe(
        debounceTime(environment.autocompleteDebounceMs),
        distinctUntilChanged(),
        switchMap((prefix) => (prefix.trim().length >= 2 ? this.merchantService.getSuggestions(prefix) : of([]))),
      )
      .subscribe((suggestions) => {
        this.suggestions = suggestions;
        this.cdr.markForCheck();
      });
  }

  onInputChange(value: string): void {
    this.valueChange.emit(value);
    this.input$.next(value);
  }

  onOptionSelected(event: MatAutocompleteSelectedEvent): void {
    const suggestion = this.suggestions.find((s) => s.name === event.option.value);
    this.value = event.option.value;
    this.valueChange.emit(this.value);
    if (suggestion) this.suggestionSelected.emit(suggestion);
  }
}
