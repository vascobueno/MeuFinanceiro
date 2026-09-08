import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button } from 'primeng/button';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';

export interface HelpDialogData {
  title: string;
  body: string;
}

/**
 * Diálogo de ajuda contextual por tela, acionado pelo ícone "?" da topbar (mf-shell).
 * Migrado de MatDialog pra PrimeNG DynamicDialog (docs/migracao-primeng/00-plano.md,
 * UI-01.3) — chamador (shell.component.ts) usa DialogService.open() no lugar de
 * MatDialog.open(), mesmo padrão programático, sem precisar de tag de host no template.
 */
@Component({
  selector: 'mf-help-dialog',
  standalone: true,
  imports: [RouterLink, Button],
  template: `
    <p>{{ config.data?.body }}</p>
    <a routerLink="/ajuda" (click)="dialogRef.close()">Ver ajuda de todas as telas →</a>
    <div class="mf-help-dialog-actions">
      <p-button label="Entendi" (onClick)="dialogRef.close()" />
    </div>
  `,
  styles: [
    `
      .mf-help-dialog-actions {
        display: flex;
        justify-content: flex-end;
        margin-top: 1rem;
      }
    `,
  ],
})
export class HelpDialogComponent {
  constructor(
    public dialogRef: DynamicDialogRef,
    public config: DynamicDialogConfig<HelpDialogData>,
  ) {}
}
