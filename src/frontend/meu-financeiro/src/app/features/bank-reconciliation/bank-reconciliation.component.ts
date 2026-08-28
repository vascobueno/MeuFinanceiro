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
import { AccountService } from '../../core/services/account.service';
import { BankReconciliationItem, BankStatementItemRequest } from '../../core/models/reconciliation.model';
import { Account } from '../../core/models/account.model';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { StatCardComponent } from '../../shared/components/stat-card.component';

/**
 * docs/09-conciliacao-conta-corrente.md: importa extrato normalizado (parser de
 * PDF/OFX fica fora do escopo desta versão — aceita-se lançamento manual dos itens ou um
 * CSV simples colado no campo de texto) e mostra candidatos de conciliação por score.
 */
@Component({
  selector: 'mf-bank-reconciliation',
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
  templateUrl: './bank-reconciliation.component.html',
  styleUrl: './bank-reconciliation.component.scss',
})
export class BankReconciliationComponent implements OnInit {
  readonly displayedColumns = ['date', 'description', 'amount', 'score', 'status', 'actions'];
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<BankReconciliationItem[]>([]);
  readonly dataSource = new MatTableDataSource<BankReconciliationItem>([]);
  readonly accounts = signal<Account[]>([]);

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
  get matchedPercent(): number {
    return this.items().length === 0 ? 0 : Math.round((this.matchedCount / this.items().length) * 100);
  }

  selectedAccountId: string | null = null;
  selectedStatus = '';

  readonly showImportForm = signal(false);
  importAccountId: string | null = null;
  // Date (não string) por ser ligado a [(ngModel)] de mat-datepicker — ver comentário em
  // transaction-form.component.ts sobre o loop infinito de change detection que uma
  // conversão string->Date refeita a cada leitura provoca sob Angular zoneless.
  referenceStart: Date | null = null;
  referenceEnd: Date | null = null;
  openingBalance: number | null = null;
  closingBalance: number | null = null;
  /** Uma linha por movimento: data;descrição;tipo;valor (valor com sinal: + entrada, - saída). */
  itemsRaw = '';
  readonly importing = signal(false);
  readonly importResult = signal<string | null>(null);

  constructor(
    private readonly reconciliationService: ReconciliationService,
    private readonly accountService: AccountService,
    readonly authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.accountService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => {
      this.accounts.set(r.items);
      if (r.items.length > 0) this.importAccountId = r.items[0].id;
    });
    this.load();
  }


  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.reconciliationService.getBankReconciliations(this.selectedAccountId ?? undefined, this.selectedStatus || undefined).subscribe({
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

  parseItems(): BankStatementItemRequest[] {
    return this.itemsRaw
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0)
      .map((line) => {
        const [date, description, itemType, amount] = line.split(';').map((v) => v.trim());
        return { date, description, itemType: itemType || 'OTHER_EXPENSE', amount: Number(amount) };
      });
  }

  submitImport(): void {
    if (!this.importAccountId || this.openingBalance == null || this.closingBalance == null) {
      this.importResult.set('Preencha conta, saldo inicial e saldo final.');
      return;
    }

    const items = this.parseItems();
    if (items.length === 0) {
      this.importResult.set('Informe ao menos um movimento.');
      return;
    }

    this.importing.set(true);
    this.reconciliationService
      .importBankStatement({
        accountId: this.importAccountId,
        referenceStart: this.toIsoDate(this.referenceStart) ?? '',
        referenceEnd: this.toIsoDate(this.referenceEnd) ?? '',
        openingBalance: this.openingBalance,
        closingBalance: this.closingBalance,
        items,
      })
      .subscribe({
        next: (statement) => {
          this.importing.set(false);
          this.importResult.set(
            statement.balanceValidated
              ? `Extrato importado (${statement.itemCount} itens). Saldo inicial + movimentos fecha com o saldo final. ✅`
              : `Extrato importado (${statement.itemCount} itens), mas a soma dos movimentos não fechou com o saldo final informado. Revise os valores. ⚠️`,
          );
          this.itemsRaw = '';
          this.load();
        },
        error: () => {
          this.importing.set(false);
          this.importResult.set('Não foi possível importar o extrato.');
        },
      });
  }

  accept(item: BankReconciliationItem): void {
    this.reconciliationService.acceptBankReconciliation(item.id).subscribe({ next: () => this.load() });
  }

  undo(item: BankReconciliationItem): void {
    this.reconciliationService.undoBankReconciliation(item.id).subscribe({ next: () => this.load() });
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
