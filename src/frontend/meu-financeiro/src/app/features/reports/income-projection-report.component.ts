import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonDirective } from 'primeng/button';
import { ProjectionService } from '../../core/services/projection.service';
import { ProjectionEntry } from '../../core/models/projection.model';
import { TransactionType } from '../../core/models/transaction.model';
import { ListStateComponent } from '../../shared/components/list-state.component';

/**
 * Reaproveita o mesmo endpoint/serviço da tela Projeção (só filtra Type === Income no
 * frontend — nenhum endpoint novo no backend, ver ProjectionEntry.type). Imprimível via
 * window.print() + CSS de impressão compartilhado (styles.scss).
 */
@Component({
  selector: 'mf-income-projection-report',
  standalone: true,
  imports: [CommonModule, ButtonDirective, ListStateComponent],
  templateUrl: './income-projection-report.component.html',
  styleUrl: './income-projection-report.component.scss',
})
export class IncomeProjectionReportComponent implements OnInit {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly entries = signal<ProjectionEntry[]>([]);
  readonly generatedAt = new Date();

  readonly total = computed(() => this.entries().reduce((sum, e) => sum + e.amount, 0));

  constructor(private readonly projectionService: ProjectionService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    const horizon = new Date();
    horizon.setDate(horizon.getDate() + 90);
    const date = horizon.toISOString().slice(0, 10);

    this.projectionService.getProjection(date).subscribe({
      next: (projection) => {
        this.entries.set(projection.timeline.filter((e) => e.type === TransactionType.Income));
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar a projeção de receitas.');
        this.loading.set(false);
      },
    });
  }

  print(): void {
    window.print();
  }
}
