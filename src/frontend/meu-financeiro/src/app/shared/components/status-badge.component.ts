import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatChipsModule } from '@angular/material/chips';
import { TransactionStatus } from '../../core/models/transaction.model';

@Component({
  selector: 'mf-status-badge',
  standalone: true,
  imports: [CommonModule, MatChipsModule],
  template: `
    <mat-chip-set>
      <mat-chip [ngClass]="badgeClass" disableRipple>{{ label }}</mat-chip>
    </mat-chip-set>
  `,
  styles: [
    `
      :host ::ng-deep mat-chip-set {
        display: inline-flex;
      }
      :host ::ng-deep .mat-mdc-chip {
        cursor: default;
      }
      :host ::ng-deep .mat-mdc-chip.mf-badge-ok {
        --mat-chip-elevated-container-color: #e8f7f1;
        --mat-chip-label-text-color: #087957;
      }
      :host ::ng-deep .mat-mdc-chip.mf-badge-warn {
        --mat-chip-elevated-container-color: #fff5dc;
        --mat-chip-label-text-color: #946000;
      }
      :host ::ng-deep .mat-mdc-chip.mf-badge-neutral {
        --mat-chip-elevated-container-color: #eef1f5;
        --mat-chip-label-text-color: #626d7f;
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
