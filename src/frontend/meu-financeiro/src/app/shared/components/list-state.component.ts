import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatButtonModule } from '@angular/material/button';

/**
 * Estado padrão de loading/erro/vazio para grids (docs/copilot-instructions.md — seção UI:
 * "Todas as grids relevantes devem ter filtros, ordenação, paginação, loading, erro e empty state.").
 */
@Component({
  selector: 'mf-list-state',
  standalone: true,
  imports: [CommonModule, MatProgressSpinnerModule, MatButtonModule],
  template: `
    @if (loading) {
      <div class="mf-list-state-spinner">
        <mat-progress-spinner diameter="40" mode="indeterminate"></mat-progress-spinner>
      </div>
    }

    @if (!loading && error) {
      <div class="mf-list-state-error" role="alert">
        <span>{{ error }}</span>
        @if (onRetry) {
          <button mat-stroked-button color="warn" type="button" (click)="onRetry()">Tentar novamente</button>
        }
      </div>
    }

    @if (!loading && !error && empty) {
      <div class="mf-empty-state">
        <p class="mb-1">{{ emptyMessage }}</p>
        <ng-content></ng-content>
      </div>
    }
  `,
  styles: [
    `
      .mf-list-state-spinner {
        display: flex;
        justify-content: center;
        padding: 3rem 0;
      }
      .mf-list-state-error {
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 1rem;
        background: rgba(192, 57, 43, 0.1);
        color: var(--mf-red, #c0392b);
        border-radius: 0.5rem;
        padding: 0.75rem 1rem;
      }
    `,
  ],
})
export class ListStateComponent {
  @Input() loading = false;
  @Input() error: string | null = null;
  @Input() empty = false;
  @Input() emptyMessage = 'Nenhum item encontrado.';
  @Input() onRetry?: () => void;
}
