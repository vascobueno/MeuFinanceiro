import { Component, Inject } from '@angular/core';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';

export interface HelpDialogData {
  title: string;
  body: string;
}

/** Diálogo de ajuda contextual por tela, acionado pelo ícone "?" da topbar (mf-shell). */
@Component({
  selector: 'mf-help-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, RouterLink],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <p>{{ data.body }}</p>
      <a routerLink="/ajuda" [mat-dialog-close]="true">Ver ajuda de todas as telas →</a>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-flat-button color="primary" [mat-dialog-close]="true">Entendi</button>
    </mat-dialog-actions>
  `,
})
export class HelpDialogComponent {
  constructor(
    public dialogRef: MatDialogRef<HelpDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: HelpDialogData,
  ) {}
}
