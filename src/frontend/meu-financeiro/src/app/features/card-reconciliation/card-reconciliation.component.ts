import { Component, OnInit, ViewChild, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule, MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { AuthService } from '../../core/services/auth.service';
import { ReconciliationService } from '../../core/services/reconciliation.service';
import { CardService } from '../../core/services/card.service';
import { CardInvoiceItemRequest, CardReconciliationItem } from '../../core/models/reconciliation.model';
import { CreditCard } from '../../core/models/card.model';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { StatCardComponent } from '../../shared/components/stat-card.component';

/** docs/08-conciliacao-cartoes.md — entrada inicial: PDF da fatura (normalizado aqui manualmente). */
@Component({
  selector: 'mf-card-reconciliation',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatIconModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatTableModule,
    MatSortModule,
    ListStateComponent,
    StatCardComponent,
  ],
  providers: [
    provideNativeDateAdapter(),
    { provide: MAT_DATE_LOCALE, useValue: 'pt-BR' },
    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { floatLabel: 'always', subscriptSizing: 'dynamic' } },
  ],
  templateUrl: './card-reconciliation.component.html',
  styleUrl: './card-reconciliation.component.scss',
})
export class CardReconciliationComponent implements OnInit {
  readonly displayedColumns = ['date', 'description', 'amount', 'score', 'status', 'actions'];
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<CardReconciliationItem[]>([]);
  readonly dataSource = new MatTableDataSource<CardReconciliationItem>([]);
  readonly cards = signal<CreditCard[]>([]);

  // Setter (não ngAfterViewInit) — a <table> só existe depois que loading() vira false, então
  // um @ViewChild lido em ngAfterViewInit pegaria undefined; o setter dispara de novo quando ela aparece.
  @ViewChild(MatSort) set sort(sort: MatSort) {
    if (sort) this.dataSource.sort = sort;
  }

  // KPIs calculados a partir dos itens já carregados na tela (sem endpoint de agregação novo).
  get matchedCount(): number {
    return this.items().filter((i) => i.status === 'Matched' || i.status === 'ManuallyMatched').length;
  }
  get possibleCount(): number {
    return this.items().filter((i) => i.status === 'PossibleMatch').length;
  }
  get unmatchedCount(): number {
    return this.items().filter((i) => i.status === 'Unmatched').length;
  }

  selectedCardId: string | null = null;
  selectedStatus = '';

  readonly showImportForm = signal(false);
  importCardId: string | null = null;
  // Date (não string) por ser ligado a [(ngModel)] de mat-datepicker — ver comentário em
  // transaction-form.component.ts sobre o loop infinito de change detection que uma
  // conversão string->Date refeita a cada leitura provoca sob Angular zoneless.
  closingDate: Date | null = null;
  dueDate: Date | null = null;
  totalAmount: number | null = null;
  /** Uma linha por item: data;descrição;parcela;valor */
  itemsRaw = '';
  readonly importing = signal(false);
  readonly importResult = signal<string | null>(null);

  constructor(
    private readonly reconciliationService: ReconciliationService,
    private readonly cardService: CardService,
    readonly authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.cardService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => {
      this.cards.set(r.items);
      if (r.items.length > 0) this.importCardId = r.items[0].id;
    });
    this.load();
  }


  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.reconciliationService.getCardReconciliations(this.selectedCardId ?? undefined, this.selectedStatus || undefined).subscribe({
      next: (items) => {
        this.items.set(items);
        this.dataSource.data = items;
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar as conciliações.');
        this.loading.set(false);
      },
    });
  }

  parseItems(): CardInvoiceItemRequest[] {
    return this.itemsRaw
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0)
      .map((line) => {
        const [date, description, installmentLabel, amount] = line.split(';').map((v) => v.trim());
        return { date, description, installmentLabel: installmentLabel || undefined, amount: Number(amount) };
      });
  }

  submitImport(): void {
    if (!this.importCardId || this.totalAmount == null) {
      this.importResult.set('Preencha o cartão e o valor total da fatura.');
      return;
    }

    const items = this.parseItems();
    if (items.length === 0) {
      this.importResult.set('Informe ao menos um item da fatura.');
      return;
    }

    this.importing.set(true);
    this.reconciliationService
      .importCardInvoice({
        creditCardId: this.importCardId,
        closingDate: this.toIsoDate(this.closingDate) ?? '',
        dueDate: this.toIsoDate(this.dueDate) ?? '',
        totalAmount: this.totalAmount,
        items,
      })
      .subscribe({
        next: (invoice) => {
          this.importing.set(false);
          this.importResult.set(
            invoice.totalValidated
              ? `Fatura importada (${invoice.itemCount} itens) e o total confere. ✅`
              : `Fatura importada (${invoice.itemCount} itens), mas a soma dos itens não fechou com o total informado. ⚠️`,
          );
          this.itemsRaw = '';
          this.load();
        },
        error: () => {
          this.importing.set(false);
          this.importResult.set('Não foi possível importar a fatura.');
        },
      });
  }

  accept(item: CardReconciliationItem): void {
    this.reconciliationService.acceptCardReconciliation(item.id).subscribe({ next: () => this.load() });
  }

  undo(item: CardReconciliationItem): void {
    this.reconciliationService.undoCardReconciliation(item.id).subscribe({ next: () => this.load() });
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'Matched':
        return 'Conciliado';
      case 'PossibleMatch':
        return 'Possível match';
      case 'ManuallyMatched':
        return 'Conciliado manualmente';
      case 'Unmatched':
        return 'Sem correspondência';
      default:
        return status;
    }
  }

  statusClass(status: string): string {
    switch (status) {
      case 'Matched':
      case 'ManuallyMatched':
        return 'mf-badge-ok';
      case 'PossibleMatch':
        return 'mf-badge-warn';
      default:
        return 'mf-badge-neutral';
    }
  }

  private toIsoDate(value: Date | null): string | null {
    if (!value) return null;
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
