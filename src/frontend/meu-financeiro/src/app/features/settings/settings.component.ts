import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { ImportService } from '../../core/services/import.service';
import { ThemeService } from '../../core/services/theme.service';
import { ConfirmImportResult, ImportFileHistoryItem, ImportPreview } from '../../core/models/import.model';
import { ListStateComponent } from '../../shared/components/list-state.component';

/**
 * Configurações + Importação Inicial do Excel (docs/16-importacao-inicial.md).
 * Fluxo: Upload -> Validação/Prévia -> Confirmação -> Relatório.
 * Regra 1: o Excel só serve para a migração inicial; depois disso, novos lançamentos são
 * feitos pelo sistema (regra 2).
 */
@Component({
  selector: 'mf-settings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatIconModule,
    MatCardModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatTableModule,
    ListStateComponent,
  ],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss',
})
export class SettingsComponent implements OnInit {
  readonly previewColumns = ['rowNumber', 'date', 'description', 'type', 'origin', 'category', 'amount', 'status'];
  readonly historyColumns = ['fileName', 'status', 'date', 'imported', 'rejected'];
  readonly history = signal<ImportFileHistoryItem[]>([]);
  readonly loadingHistory = signal(true);

  selectedFile: File | null = null;
  readonly uploading = signal(false);
  readonly uploadError = signal<string | null>(null);
  readonly preview = signal<ImportPreview | null>(null);

  readonly confirming = signal(false);
  readonly confirmResult = signal<ConfirmImportResult | null>(null);

  constructor(
    private readonly importService: ImportService,
    readonly themeService: ThemeService,
  ) {}

  ngOnInit(): void {
    this.loadHistory();
  }

  loadHistory(): void {
    this.loadingHistory.set(true);
    this.importService.getHistory().subscribe({
      next: (history) => {
        this.history.set(history);
        this.loadingHistory.set(false);
      },
      error: () => this.loadingHistory.set(false),
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
    this.preview.set(null);
    this.confirmResult.set(null);
    this.uploadError.set(null);
  }

  upload(force = false): void {
    if (!this.selectedFile) return;

    this.uploading.set(true);
    this.uploadError.set(null);
    this.confirmResult.set(null);

    this.importService.uploadAndValidate(this.selectedFile, force).subscribe({
      next: (preview) => {
        this.uploading.set(false);
        this.preview.set(preview);
      },
      error: () => {
        this.uploading.set(false);
        this.uploadError.set('Não foi possível processar a planilha. Verifique o formato do arquivo.');
      },
    });
  }

  confirmImport(): void {
    const importFileId = this.preview()?.importFileId;
    if (!importFileId) return;

    this.confirming.set(true);
    this.importService.confirm(importFileId).subscribe({
      next: (result) => {
        this.confirming.set(false);
        this.confirmResult.set(result);
        this.preview.set(null);
        this.selectedFile = null;
        this.loadHistory();
      },
      error: () => {
        this.confirming.set(false);
        this.uploadError.set('Não foi possível confirmar a importação.');
      },
    });
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'Uploaded':
        return 'Enviado';
      case 'Validated':
        return 'Validado (aguardando confirmação)';
      case 'Imported':
        return 'Importado';
      case 'Failed':
        return 'Falhou';
      default:
        return status;
    }
  }
}
