import { Component, Input, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonDirective } from 'primeng/button';
import { Tooltip } from 'primeng/tooltip';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { ProgressSpinner } from 'primeng/progressspinner';
import { forkJoin, of } from 'rxjs';
import { AttachmentService } from '../../core/services/attachment.service';
import {
  ATTACHMENT_ACCEPT,
  ATTACHMENT_MAX_BYTES,
  Attachment,
  AttachmentOwner,
  AttachmentOwnerKind,
} from '../../core/models/attachment.model';

/**
 * Lista e envia anexos de um lançamento ou parcelamento — nota fiscal, boleto, comprovante.
 *
 * Funciona em dois modos, porque a tela de criação ainda não tem a que anexar:
 * - com `ownerId`, fala direto com a API (edição do lançamento, detalhe do parcelamento);
 * - sem `ownerId`, só guarda os arquivos escolhidos, e o formulário chama `uploadPending()`
 *   depois que o save devolve o id. É por isso que o backend passou a devolver
 *   InstallmentPlanResponse.Id e TransactionResponse.InstallmentPlanId.
 */
@Component({
  selector: 'mf-attachments',
  standalone: true,
  imports: [CommonModule, ButtonDirective, Tooltip, ConfirmDialog, ProgressSpinner],
  providers: [ConfirmationService],
  template: `
    <p-confirm-dialog />

    <div class="mf-attachments">
      <label class="mf-field-label">Anexos</label>

      @if (loading()) {
        <p-progress-spinner styleClass="mf-inline-spinner" strokeWidth="4" />
      }

      @if (error(); as message) {
        <div class="mf-form-error" role="alert">{{ message }}</div>
      }

      @if (!loading() && items().length === 0 && pending().length === 0) {
        <p class="mf-attachments-empty">Nenhum arquivo anexado.</p>
      }

      <ul class="mf-attachments-list">
        @for (item of items(); track item.id) {
          <li class="mf-attachments-item">
            <i class="pi" [class]="iconFor(item.contentType)" aria-hidden="true"></i>
            <span class="mf-attachments-name" [title]="item.fileName">{{ item.fileName }}</span>
            <span class="mf-attachments-size">{{ formatSize(item.sizeBytes) }}</span>

            <button
              type="button"
              class="mf-row-edit-icon"
              pTooltip="Baixar"
              tooltipPosition="top"
              [attr.aria-label]="'Baixar ' + item.fileName"
              (click)="download(item)"
            >
              <i class="pi pi-download"></i>
            </button>

            @if (!isReadonly) {
              <button
                type="button"
                class="mf-row-delete-icon"
                pTooltip="Excluir"
                tooltipPosition="top"
                [attr.aria-label]="'Excluir ' + item.fileName"
                (click)="confirmRemove(item)"
              >
                <i class="pi pi-trash"></i>
              </button>
            }
          </li>
        }

        @for (file of pending(); track file.name) {
          <li class="mf-attachments-item mf-attachments-pending">
            <i class="pi pi-clock" aria-hidden="true"></i>
            <span class="mf-attachments-name" [title]="file.name">{{ file.name }}</span>
            <span class="mf-attachments-size">{{ formatSize(file.size) }}</span>
            <span class="mf-attachments-hint">será enviado ao salvar</span>

            <button
              type="button"
              class="mf-row-delete-icon"
              pTooltip="Remover"
              tooltipPosition="top"
              [attr.aria-label]="'Remover ' + file.name"
              (click)="removePending(file)"
            >
              <i class="pi pi-times"></i>
            </button>
          </li>
        }
      </ul>

      @if (!isReadonly) {
        <input
          #fileInput
          type="file"
          [accept]="accept"
          multiple
          (change)="onFilesSelected($event)"
          hidden
        />
        <button
          pButton
          type="button"
          [outlined]="true"
          size="small"
          [disabled]="uploading()"
          (click)="fileInput.click()"
        >
          <i class="pi pi-paperclip"></i>
          Anexar arquivo
        </button>
        <span class="mf-attachments-hint">PDF ou imagem, até 10 MB</span>
      }
    </div>
  `,
  styles: [
    `
      .mf-attachments {
        display: flex;
        flex-direction: column;
        align-items: flex-start;
        gap: 0.5rem;
      }
      .mf-attachments-list {
        list-style: none;
        margin: 0;
        padding: 0;
        width: 100%;
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
      }
      .mf-attachments-item {
        display: flex;
        align-items: center;
        gap: 0.625rem;
        padding: 0.4rem 0.625rem;
        border: 1px solid var(--mf-border, #e0e0e0);
        border-radius: 0.5rem;
      }
      .mf-attachments-pending {
        border-style: dashed;
        opacity: 0.85;
      }
      .mf-attachments-name {
        flex: 1;
        min-width: 0;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .mf-attachments-size,
      .mf-attachments-hint {
        color: var(--mf-text-muted, #6b7280);
        font-size: 0.8125rem;
        white-space: nowrap;
      }
      .mf-attachments-empty {
        margin: 0;
        color: var(--mf-text-muted, #6b7280);
        font-size: 0.875rem;
      }
    `,
  ],
})
export class AttachmentsComponent {
  private readonly attachmentService = inject(AttachmentService);
  private readonly confirmationService = inject(ConfirmationService);

  @Input() ownerKind: AttachmentOwnerKind = 'transaction';
  /**
   * Nome `isReadonly`, não `readonly`: `readonly` é atributo nativo de input e o Angular o
   * normaliza para a propriedade DOM `readOnly`, então o binding não chegaria ao componente.
   */
  @Input() isReadonly = false;

  /** Quando null, o componente fica em modo "pendente" — ver uploadPending(). */
  @Input()
  set ownerId(value: string | null | undefined) {
    this.currentOwnerId = value ?? null;
    if (this.currentOwnerId) this.load();
  }

  readonly accept = ATTACHMENT_ACCEPT;
  readonly items = signal<Attachment[]>([]);
  readonly pending = signal<File[]>([]);
  readonly loading = signal(false);
  readonly uploading = signal(false);
  readonly error = signal<string | null>(null);

  private currentOwnerId: string | null = null;

  /** Há arquivos escolhidos aguardando o save do formulário. */
  hasPending(): boolean {
    return this.pending().length > 0;
  }

  /**
   * Envia os arquivos segurados no modo pendente, agora que o dono existe. Chamado pelo
   * formulário no callback de sucesso do save.
   */
  uploadPending(owner: AttachmentOwner) {
    const files = this.pending();
    if (files.length === 0) return of([]);

    this.uploading.set(true);
    return forkJoin(files.map((file) => this.attachmentService.upload(owner, file)));
  }

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const chosen = Array.from(input.files ?? []);
    input.value = ''; // permite reescolher o mesmo arquivo depois de remover

    this.error.set(null);

    const tooBig = chosen.filter((file) => file.size > ATTACHMENT_MAX_BYTES);
    if (tooBig.length > 0) {
      this.error.set(`Arquivo acima de 10 MB: ${tooBig.map((f) => f.name).join(', ')}.`);
    }

    const accepted = chosen.filter((file) => file.size <= ATTACHMENT_MAX_BYTES);
    if (accepted.length === 0) return;

    if (!this.currentOwnerId) {
      this.pending.update((current) => [...current, ...accepted]);
      return;
    }

    this.uploading.set(true);
    const owner: AttachmentOwner = { kind: this.ownerKind, id: this.currentOwnerId };
    forkJoin(accepted.map((file) => this.attachmentService.upload(owner, file))).subscribe({
      next: () => {
        this.uploading.set(false);
        this.load();
      },
      error: (err) => {
        this.uploading.set(false);
        this.error.set(err?.error?.detail ?? err?.error?.title ?? 'Não foi possível enviar o arquivo.');
      },
    });
  }

  removePending(file: File): void {
    this.pending.update((current) => current.filter((f) => f !== file));
  }

  confirmRemove(item: Attachment): void {
    this.confirmationService.confirm({
      header: 'Excluir anexo',
      message: `Excluir "${item.fileName}"? Esta ação não pode ser desfeita.`,
      acceptLabel: 'Excluir',
      rejectLabel: 'Cancelar',
      accept: () => this.remove(item),
    });
  }

  download(item: Attachment): void {
    // Via blob, e não por link direto: o endpoint exige o header de autenticação do interceptor.
    this.attachmentService.download(item.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = item.fileName;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.error.set('Não foi possível baixar o arquivo.'),
    });
  }

  iconFor(contentType: string): string {
    return contentType === 'application/pdf' ? 'pi-file-pdf' : 'pi-image';
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  private load(): void {
    if (!this.currentOwnerId) return;

    this.loading.set(true);
    this.attachmentService.list({ kind: this.ownerKind, id: this.currentOwnerId }).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar os anexos.');
        this.loading.set(false);
      },
    });
  }

  private remove(item: Attachment): void {
    this.attachmentService.delete(item.id).subscribe({
      next: () => this.items.update((current) => current.filter((a) => a.id !== item.id)),
      error: () => this.error.set('Não foi possível excluir o anexo.'),
    });
  }
}
