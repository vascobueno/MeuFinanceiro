import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { Table, SortIcon, SortableColumn } from 'primeng/table';
import { ConfirmationService, SortEvent } from 'primeng/api';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { Tooltip } from 'primeng/tooltip';
import { AuthService } from '../../core/services/auth.service';
import { businessRuleOf } from '../../core/services/problem.util';
import { StatementService } from '../../core/services/statement.service';
import { AccountService } from '../../core/services/account.service';
import { BankStatement, BankStatementFilter } from '../../core/models/statement.model';
import { Account } from '../../core/models/account.model';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { PaginationComponent } from '../../shared/components/pagination.component';
import { DateRange, DateRangeComponent } from '../../shared/components/date-range.component';
import { nextSort } from '../../core/services/sort.util';

/**
 * Tela Extratos — a irmã de Faturas do lado da conta: lista o que a importação da conciliação
 * bancária gravou, com filtros/ordenação/paginação server-side (regra 19).
 *
 * Só lê. Importar extrato continua em Contas › Conciliação.
 */
@Component({
  selector: 'mf-statement-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MfIconComponent,
    InputText,
    Select,
    Table,
    SortIcon,
    SortableColumn,
    ListStateComponent,
    PaginationComponent,
    DateRangeComponent,
    ConfirmDialog,
    Tooltip,
  ],
  providers: [ConfirmationService],
  templateUrl: './statement-list.component.html',
  styleUrl: './statement-list.component.scss',
})
export class StatementListComponent implements OnInit {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly statements = signal<BankStatement[]>([]);
  readonly totalPages = signal(0);
  readonly totalItems = signal(0);

  readonly accounts = signal<Account[]>([]);
  readonly accountOptions = computed(() => [
    { label: 'Todas', value: null },
    ...this.accounts().map((a) => ({ label: a.name, value: a.id })),
  ]);
  readonly period: DateRange = { start: null, end: null };

  readonly filter: BankStatementFilter = {
    page: 1,
    pageSize: 15,
    sortBy: 'periodo',
    sortDir: 'desc',
    accountId: null,
    periodStart: null,
    periodEnd: null,
    minAmount: null,
    maxAmount: null,
  };

  constructor(
    private readonly statementService: StatementService,
    private readonly confirmationService: ConfirmationService,
    readonly authService: AuthService,
    private readonly accountService: AccountService,
  ) {}

  ngOnInit(): void {
    this.accountService.getAll({ page: 1, pageSize: 100 }).subscribe((r) => this.accounts.set(r.items));
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.statementService.getAll(this.filter).subscribe({
      next: (result) => {
        this.statements.set(result.items);
        this.totalPages.set(result.totalPages);
        this.totalItems.set(result.totalItems);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar os extratos.');
        this.loading.set(false);
      },
    });
  }

  applyFilters(): void {
    this.filter.page = 1;
    this.load();
  }

  onPeriodChange(range: DateRange): void {
    this.period.start = range.start;
    this.period.end = range.end;
    this.filter.periodStart = range.start;
    this.filter.periodEnd = range.end;
    this.applyFilters();
  }

  onPageChange(page: number): void {
    this.filter.page = page;
    this.load();
  }

  onSort(event: SortEvent): void {
    // A direção vem de nextSort, e não do `order` do evento: a tabela é recriada a cada carga
    // e o p-table emite sempre ascendente (core/services/sort.util.ts).
    const proxima = nextSort(this.filter, event.field, 'periodo');
    this.filter.sortBy = proxima.sortBy;
    this.filter.sortDir = proxima.sortDir;
    this.load();
  }

  /**
   * Exclui o extrato importado por engano. O servidor recusa se algum item já levou um
   * lançamento a "conciliado" — a mensagem dele diz o que fazer.
   */
  confirmRemove(id: string): void {
    this.confirmationService.confirm({
      header: 'Excluir extrato',
      message: 'Excluir o extrato importado, com seus itens e as sugestões de conciliação? Esse ação não pode ser desfeita.',
      acceptLabel: 'Excluir',
      rejectLabel: 'Cancelar',
      accept: () =>
        this.statementService.delete(id).subscribe({
          next: () => this.load(),
          error: (err) => this.error.set(businessRuleOf(err)?.message ?? 'Não foi possível excluir o extrato.'),
        }),
    });
  }
}
