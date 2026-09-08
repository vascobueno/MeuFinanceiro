import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProgressSpinner } from 'primeng/progressspinner';
import { Button } from 'primeng/button';

/**
 * Estado padrão de loading/erro/vazio para grids (docs/copilot-instructions.md — seção UI:
 * "Todas as grids relevantes devem ter filtros, ordenação, paginação, loading, erro e empty
 * state."). Migrado de mat-progress-spinner/mat-stroked-button pra PrimeNG
 * (docs/migracao-primeng/00-plano.md, UI-01.2).
 */
@Component({
  selector: 'mf-list-state',
  standalone: true,
  imports: [CommonModule, ProgressSpinner, Button],
  template: `
    @if (loading) {
      <div class="mf-list-state-spinner">
        <p-progress-spinner styleClass="mf-list-state-spinner-icon" strokeWidth="4" />
      </div>
    }

    @if (!loading && error) {
      <div class="mf-list-state-error" role="alert">
        <span>{{ error }}</span>
        @if (onRetry) {
          <p-button label="Tentar novamente" severity="danger" [outlined]="true" (onClick)="onRetry()" />
        }
      </div>
    }

    @if (!loading && !error && empty) {
      <div class="mf-empty-state">
        <p class="mf-empty-state-message">{{ emptyMessage }}</p>
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
      .mf-list-state-spinner-icon {
        width: 40px;
        height: 40px;
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
      .mf-empty-state-message {
        margin: 0 0 1.25rem;
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
