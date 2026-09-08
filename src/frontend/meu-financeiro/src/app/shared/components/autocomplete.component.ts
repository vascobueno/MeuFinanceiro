import { ChangeDetectorRef, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AutoComplete, AutoCompleteCompleteEvent, AutoCompleteSelectEvent } from 'primeng/autocomplete';
import { environment } from '../../../environments/environment';
import { MerchantSuggestion } from '../../core/models/merchant.model';
import { MerchantService } from '../../core/services/merchant.service';

/**
 * Autocomplete de descrição por prefixo (RF005, regra 18). Debounce ~300ms e no máximo
 * 10-15 sugestões (docs/06-lancamentos.md, .github/copilot-instructions.md). Migrado de
 * mat-autocomplete pra PrimeNG AutoComplete (docs/migracao-primeng/00-plano.md, UI-01.4).
 *
 * Sem `optionValue`: o `AutoComplete.updateModel()` do PrimeNG aplica `getOptionValue()` em
 * QUALQUER valor passado pra ele — inclusive o texto puro que o usuário está digitando, não
 * só a sugestão selecionada. Com `optionValue="name"` (tentativa original), digitar texto
 * livre (sem escolher uma sugestão) fazia `getOptionValue()` tentar ler `"texto".name` — uma
 * string não tem esse campo, então o valor virava `undefined` sempre que o usuário não
 * selecionava uma sugestão da lista (o caso mais comum: a maioria das descrições não bate
 * com um merchant conhecido). Bug crítico só encontrado em QA end-to-end na Fase 5
 * (submeter o formulário de lançamento quebrava com "Cannot read properties of undefined
 * (reading 'trim')") — a Fase 1 só tinha testado o fluxo de selecionar uma sugestão.
 * Sem `optionValue`, `getOptionValue()` devolve o valor como está quando é string (texto
 * digitado) ou o objeto `MerchantSuggestion` inteiro quando uma sugestão é selecionada
 * (não tem campo `.value`) — por isso `onModelChange()` normaliza os dois casos pra sempre
 * emitir uma string simples pro `[(value)]` do componente pai.
 */
@Component({
  selector: 'mf-description-autocomplete',
  standalone: true,
  imports: [CommonModule, FormsModule, AutoComplete],
  template: `
    <label class="mf-autocomplete-label">Descrição</label>
    <p-autocomplete
      class="mf-autocomplete-field"
      [ngModel]="value"
      [suggestions]="suggestions"
      optionLabel="name"
      [minQueryLength]="2"
      [delay]="autocompleteDebounceMs"
      placeholder="Ex.: Amazon"
      (completeMethod)="onComplete($event)"
      (onSelect)="onOptionSelected($event)"
      (ngModelChange)="onModelChange($event)"
    />
  `,
  styles: [
    `
      .mf-autocomplete-label {
        display: block;
        font-size: 0.75rem;
        font-weight: 600;
        color: var(--mf-muted, #8b8175);
        margin-bottom: 0.3rem;
      }
      .mf-autocomplete-field {
        display: block;
        width: 100%;
      }
      .mf-autocomplete-field ::ng-deep .p-autocomplete-input {
        width: 100%;
      }
    `,
  ],
})
export class AutocompleteComponent {
  @Input() value = '';
  @Output() valueChange = new EventEmitter<string>();
  @Output() suggestionSelected = new EventEmitter<MerchantSuggestion>();

  readonly autocompleteDebounceMs = environment.autocompleteDebounceMs;

  suggestions: MerchantSuggestion[] = [];

  constructor(
    private readonly merchantService: MerchantService,
    private readonly cdr: ChangeDetectorRef,
  ) {}

  onModelChange(value: string | MerchantSuggestion | null): void {
    const normalized = typeof value === 'string' ? value : (value?.name ?? '');
    this.value = normalized;
    this.valueChange.emit(normalized);
  }

  onComplete(event: AutoCompleteCompleteEvent): void {
    const prefix = event.query.trim();
    if (prefix.length < 2) {
      this.suggestions = [];
      return;
    }
    // Resposta HTTP chega numa macrotask própria, fora do evento (completeMethod) que
    // disparou a chamada — sob zoneless, é preciso marcar a view manualmente ou a lista de
    // sugestões nunca aparece (mesmo motivo documentado no Material antes da migração).
    this.merchantService.getSuggestions(prefix).subscribe((suggestions) => {
      this.suggestions = suggestions;
      this.cdr.markForCheck();
    });
  }

  onOptionSelected(event: AutoCompleteSelectEvent): void {
    const selectedName = typeof event.value === 'string' ? event.value : event.value?.name;
    const suggestion = this.suggestions.find((s) => s.name === selectedName);
    if (suggestion) this.suggestionSelected.emit(suggestion);
  }
}
