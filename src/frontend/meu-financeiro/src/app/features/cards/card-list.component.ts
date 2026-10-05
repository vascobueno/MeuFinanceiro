import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { ButtonDirective } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Table, SortIcon, SortableColumn } from 'primeng/table';
import { Tooltip } from 'primeng/tooltip';
import { SortEvent } from 'primeng/api';
import { AuthService } from '../../core/services/auth.service';
import { CardService } from '../../core/services/card.service';
import { InvoiceService } from '../../core/services/invoice.service';
import { CreateCardRequest, CreditCard } from '../../core/models/card.model';
import { CardInvoiceStatus } from '../../core/models/invoice.model';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { PaginationComponent } from '../../shared/components/pagination.component';
import { StatCardComponent } from '../../shared/components/stat-card.component';
import { nextSort } from '../../core/services/sort.util';

/** Tela de cartões (RF003), espelhando docs/screens/cartoes/mockup.html. */
@Component({
  selector: 'mf-card-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MfIconComponent,
    ButtonDirective,
    InputText,
    Table,
    SortIcon,
    SortableColumn,
    ListStateComponent,
    PaginationComponent,
    StatCardComponent,
    ConfirmDialog,
    Tooltip,
  ],
  providers: [ConfirmationService],
  templateUrl: './card-list.component.html',
  styleUrl: './card-list.component.scss',
})
export class CardListComponent implements OnInit {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly cards = signal<CreditCard[]>([]);
  readonly page = signal(1);
  readonly totalPages = signal(0);
  readonly totalItems = signal(0);
  sortBy?: string;
  sortDir: 'asc' | 'desc' = 'asc';

  // KPIs (docs/screens/cartoes): busca à parte, sem paginação.
  readonly totalLimit = signal(0);
  readonly openInvoicesTotal = signal(0);

  readonly showForm = signal(false);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly newCard: CreateCardRequest = { name: '', brand: '', closingDay: 1, dueDay: 10, limit: undefined };
  private editingIsActive = true;

  get availableLimit(): number {
    return this.totalLimit() - this.openInvoicesTotal();
  }

  constructor(
    private readonly cardService: CardService,
    private readonly invoiceService: InvoiceService,
    private readonly confirmationService: ConfirmationService,
    readonly authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.loadSummary();
    this.load();
  }

  private loadSummary(): void {
    this.cardService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => {
      this.totalLimit.set(r.items.reduce((sum, c) => sum + (c.limit ?? 0), 0));
    });
    this.invoiceService.getAll({ page: 1, pageSize: 100, status: CardInvoiceStatus.Aberta }).subscribe((r) => {
      this.openInvoicesTotal.set(r.items.reduce((sum, i) => sum + i.totalAmount, 0));
    });
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.cardService.getAll({ page: this.page(), pageSize: 10, sortBy: this.sortBy, sortDir: this.sortDir }).subscribe({
      next: (result) => {
        this.cards.set(result.items);
        this.totalPages.set(result.totalPages);
        this.totalItems.set(result.totalItems);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar os cartões.');
        this.loading.set(false);
      },
    });
  }

  onSort(event: SortEvent): void {
    // A direção vem de nextSort, e não do `order` do evento: a tabela é recriada a cada carga
    // e o p-table emite sempre ascendente (core/services/sort.util.ts).
    const proxima = nextSort({ sortBy: this.sortBy, sortDir: this.sortDir }, event.field, 'name');
    this.sortBy = proxima.sortBy;
    this.sortDir = proxima.sortDir;
    this.load();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  openForm(): void {
    this.editingId.set(null);
    this.newCard.name = '';
    this.newCard.brand = '';
    this.newCard.closingDay = 1;
    this.newCard.dueDay = 10;
    this.newCard.limit = undefined;
    this.showForm.set(true);
    this.formError.set(null);
  }

  edit(card: CreditCard): void {
    this.editingId.set(card.id);
    this.editingIsActive = card.isActive;
    this.newCard.name = card.name;
    this.newCard.brand = card.brand ?? '';
    this.newCard.closingDay = card.closingDay;
    this.newCard.dueDay = card.dueDay;
    this.newCard.limit = card.limit;
    this.showForm.set(true);
    this.formError.set(null);
  }

  closeForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
  }

  submit(): void {
    if (!this.newCard.name.trim()) {
      this.formError.set('Informe o nome do cartão.');
      return;
    }

    this.saving.set(true);
    this.formError.set(null);

    const editingId = this.editingId();
    const request$ = editingId
      ? this.cardService.update(editingId, { ...this.newCard, isActive: this.editingIsActive })
      : this.cardService.create(this.newCard);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.editingId.set(null);
        this.newCard.name = '';
        this.newCard.brand = '';
        this.load();
        this.loadSummary();
      },
      error: () => {
        this.saving.set(false);
        this.formError.set(editingId ? 'Não foi possível salvar as alterações.' : 'Não foi possível salvar o cartão.');
      },
    });
  }

  confirmDelete(card: CreditCard): void {
    this.confirmationService.confirm({
      header: 'Excluir cartão',
      message: `Deseja realmente excluir '${card.name}'?`,
      acceptLabel: 'Excluir',
      rejectLabel: 'Cancelar',
      accept: () => this.delete(card),
    });
  }

  private delete(card: CreditCard): void {
    this.cardService.delete(card.id).subscribe({
      next: () => this.load(),
      error: () => {
        this.error.set('Não foi possível excluir. Cartões com lançamentos devem ser desativados.');
      },
    });
  }
}
