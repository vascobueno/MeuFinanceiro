import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { Select } from 'primeng/select';
import { AuditService } from '../../core/services/audit.service';
import { AuditLogEntry } from '../../core/models/audit.model';
import { DateRange, DateRangeComponent } from '../../shared/components/date-range.component';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { PaginationComponent } from '../../shared/components/pagination.component';

/** Relatório de auditoria (acessos + alterações), filtrável por data — Owner vê só o próprio grupo, Administrator vê todos (resolvido no backend). */
@Component({
  selector: 'mf-audit-log',
  standalone: true,
  imports: [CommonModule, FormsModule, MfIconComponent, Select, DateRangeComponent, ListStateComponent, PaginationComponent],
  templateUrl: './audit-log.component.html',
  styleUrl: './audit-log.component.scss',
})
export class AuditLogComponent implements OnInit {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly entries = signal<AuditLogEntry[]>([]);
  readonly page = signal(1);
  readonly totalPages = signal(0);
  readonly totalItems = signal(0);

  readonly actionOptions = [
    { label: 'Todos', value: null },
    { label: 'Acesso', value: 'Login' },
    { label: 'Acesso negado', value: 'LoginFailed' },
    { label: 'Criado', value: 'Added' },
    { label: 'Alterado', value: 'Modified' },
    { label: 'Excluído', value: 'Deleted' },
  ];

  readonly dateRange: DateRange = { start: null, end: null };
  action: string | null = null;

  private readonly pageSize = 20;

  constructor(private readonly auditService: AuditService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.auditService
      .getAll({
        startDate: this.dateRange.start,
        endDate: this.dateRange.end,
        action: this.action,
        page: this.page(),
        pageSize: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          this.entries.set(result.items);
          this.totalPages.set(result.totalPages);
          this.totalItems.set(result.totalItems);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Não foi possível carregar a auditoria.');
          this.loading.set(false);
        },
      });
  }

  onDateRangeChange(range: DateRange): void {
    this.dateRange.start = range.start;
    this.dateRange.end = range.end;
    this.page.set(1);
    this.load();
  }

  onActionChange(): void {
    this.page.set(1);
    this.load();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  actionLabel(action: string): string {
    switch (action) {
      case 'Login':
        return 'Acesso';
      case 'LoginFailed':
        return 'Acesso negado';
      case 'Added':
        return 'Criado';
      case 'Modified':
        return 'Alterado';
      case 'Deleted':
        return 'Excluído';
      default:
        return action;
    }
  }
}
