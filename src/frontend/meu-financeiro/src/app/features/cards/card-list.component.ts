import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule, MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { MatSortModule, Sort } from '@angular/material/sort';
import { AuthService } from '../../core/services/auth.service';
import { CardService } from '../../core/services/card.service';
import { InvoiceService } from '../../core/services/invoice.service';
import { CreateCardRequest, CreditCard } from '../../core/models/card.model';
import { CardInvoiceStatus } from '../../core/models/invoice.model';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { PaginationComponent } from '../../shared/components/pagination.component';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog.component';
import { StatCardComponent } from '../../shared/components/stat-card.component';

/** Tela de cartões (RF003), espelhando docs/screens/cartoes/mockup.html. */
@Component({
  selector: 'mf-card-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatIconModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatTableModule,
    MatSortModule,
    ListStateComponent,
    PaginationComponent,
    StatCardComponent,
  ],
  providers: [{ provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { floatLabel: 'always', subscriptSizing: 'dynamic' } }],
  templateUrl: './card-list.component.html',
  styleUrl: './card-list.component.scss',
})
export class CardListComponent implements OnInit {
  readonly displayedColumns = ['name', 'brand', 'closingDay', 'dueDay', 'limit', 'status', 'actions'];
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
  readonly newCard: CreateCardRequest = { name: '', brand: '', closingDay: 1, dueDay: 10, limit: undefined };

  get availableLimit(): number {
    return this.totalLimit() - this.openInvoicesTotal();
  }

  constructor(
    private readonly cardService: CardService,
    private readonly invoiceService: InvoiceService,
    private readonly dialog: MatDialog,
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

  onSortChange(sort: Sort): void {
    this.sortBy = sort.direction ? sort.active : undefined;
    this.sortDir = sort.direction === 'desc' ? 'desc' : 'asc';
    this.load();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  openForm(): void {
    this.showForm.set(true);
    this.formError.set(null);
  }

  closeForm(): void {
    this.showForm.set(false);
  }

  submit(): void {
    if (!this.newCard.name.trim()) {
      this.formError.set('Informe o nome do cartão.');
      return;
    }

    this.saving.set(true);
    this.formError.set(null);

    this.cardService.create(this.newCard).subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.newCard.name = '';
        this.newCard.brand = '';
        this.load();
        this.loadSummary();
      },
      error: () => {
        this.saving.set(false);
        this.formError.set('Não foi possível salvar o cartão.');
      },
    });
  }

  confirmDelete(card: CreditCard): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Excluir cartão',
          message: `Deseja realmente excluir '${card.name}'?`,
          confirmLabel: 'Excluir',
        },
      })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) this.delete(card);
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
