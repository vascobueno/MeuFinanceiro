import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TransactionStatus } from '../../core/models/transaction.model';

/**
 * Migrado de mat-chip pra um <span> próprio (docs/migracao-primeng/00-plano.md, UI-01.2) —
 * a versão Material dependia de ::ng-deep pra colorir o chip por status, e o PrimeNG não
 * expõe internals equivalentes pra "penetrar". Como o componente já era só um rótulo
 * colorido (sem interação, sem ripple, sem seleção), um <span> com classes próprias
 * reproduz o visual sem depender de nenhuma lib de UI.
 */
@Component({
  selector: 'mf-status-badge',
  standalone: true,
  imports: [CommonModule],
  template: ` <span class="mf-status-badge" [ngClass]="badgeClass">{{ label }}</span> `,
  styles: [
    `
      .mf-status-badge {
        display: inline-flex;
        align-items: center;
        border-radius: 999px;
        padding: 0.25rem 0.7rem;
        font-size: 0.75rem;
        font-weight: 600;
        line-height: 1.4;
      }
      .mf-badge-ok {
        background: #e8f7f1;
        color: #087957;
      }
      .mf-badge-warn {
        background: #fff5dc;
        color: #946000;
      }
      .mf-badge-neutral {
        background: #eef1f5;
        color: #626d7f;
      }
    `,
  ],
})
export class StatusBadgeComponent {
  @Input({ required: true }) status!: TransactionStatus;

  get label(): string {
    switch (this.status) {
      case TransactionStatus.Pending:
        return 'Pendente';
      case TransactionStatus.Confirmed:
        return 'Confirmado';
      case TransactionStatus.Reconciled:
        return 'Conciliado';
      default:
        return '—';
    }
  }

  get badgeClass(): string {
    switch (this.status) {
      case TransactionStatus.Pending:
        return 'mf-badge-warn';
      case TransactionStatus.Confirmed:
        return 'mf-badge-neutral';
      case TransactionStatus.Reconciled:
        return 'mf-badge-ok';
      default:
        return 'mf-badge-neutral';
    }
  }
}
