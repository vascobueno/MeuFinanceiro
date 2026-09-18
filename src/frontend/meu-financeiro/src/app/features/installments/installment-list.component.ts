import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { ButtonDirective } from 'primeng/button';
import { SortEvent } from 'primeng/api';
import { Table, SortIcon, SortableColumn } from 'primeng/table';
import { AuthService } from '../../core/services/auth.service';
import { InstallmentPlanService } from '../../core/services/installment-plan.service';
import { InstallmentPlanDetail, InstallmentPlanListItem } from '../../core/models/installment-plan.model';
import { InstallmentMode, TransactionStatus } from '../../core/models/transaction.model';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { PaginationComponent } from '../../shared/components/pagination.component';
import { AttachmentsComponent } from '../../shared/components/attachments.component';

/** Tela Parcelamentos: lista os planos criados, com o andamento de cada um (docs/07). */
@Component({
  selector: 'mf-installment-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MfIconComponent,
    ButtonDirective,
    Table,
    SortIcon,
    SortableColumn,
    ListStateComponent,
    PaginationComponent,
    AttachmentsComponent,
  ],
  templateUrl: './installment-list.component.html',
  styleUrl: './installment-list.component.scss',
})
export class InstallmentListComponent implements OnInit {
  readonly InstallmentMode = InstallmentMode;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly plans = signal<InstallmentPlanListItem[]>([]);
  readonly totalPages = signal(0);
  readonly totalItems = signal(0);

  /** Plano expandido na tela (id) e as parcelas já carregadas dele. */
  readonly expandedId = signal<string | null>(null);
  readonly expandedDetail = signal<InstallmentPlanDetail | null>(null);
  readonly loadingDetail = signal(false);

  // Mesmo padrão de invoice-list: objeto mutável, não signal — ver comentário sobre
  // [sortField]/[sortOrder] no template.
  readonly filter = { page: 1, pageSize: 10, sortBy: undefined as string | undefined, sortDir: 'asc' as 'asc' | 'desc' };

  constructor(
    private readonly installmentPlanService: InstallmentPlanService,
    readonly authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.installmentPlanService.getAll(this.filter).subscribe({
      next: (result) => {
        this.plans.set(result.items);
        this.totalPages.set(result.totalPages);
        this.totalItems.set(result.totalItems);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar os parcelamentos.');
        this.loading.set(false);
      },
    });
  }

  toggleDetail(plan: InstallmentPlanListItem): void {
    if (this.expandedId() === plan.id) {
      this.expandedId.set(null);
      this.expandedDetail.set(null);
      return;
    }

    this.expandedId.set(plan.id);
    this.expandedDetail.set(null);
    this.loadingDetail.set(true);

    this.installmentPlanService.getById(plan.id).subscribe({
      next: (detail) => {
        this.expandedDetail.set(detail);
        this.loadingDetail.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar as parcelas do plano.');
        this.loadingDetail.set(false);
      },
    });
  }

  onSort(event: SortEvent): void {
    this.filter.sortBy = event.field;
    this.filter.sortDir = event.order === -1 ? 'desc' : 'asc';
    this.load();
  }

  onPageChange(page: number): void {
    this.filter.page = page;
    this.load();
  }

  origin(plan: InstallmentPlanListItem): string {
    return plan.accountName || plan.creditCardName || '—';
  }

  modeLabel(mode: InstallmentMode): string {
    return mode === InstallmentMode.TotalAmount ? 'Valor total' : 'Valor da parcela';
  }

  statusLabel(status?: TransactionStatus | null): string {
    switch (status) {
      case TransactionStatus.Reconciled:
        return 'Conciliada';
      case TransactionStatus.Confirmed:
        return 'Confirmada';
      case TransactionStatus.Pending:
        return 'Pendente';
      default:
        return '—';
    }
  }

  statusClass(status?: TransactionStatus | null): string {
    switch (status) {
      case TransactionStatus.Reconciled:
        return 'mf-badge-ok';
      case TransactionStatus.Confirmed:
        return 'mf-badge-warn';
      default:
        return 'mf-badge-neutral';
    }
  }
}
