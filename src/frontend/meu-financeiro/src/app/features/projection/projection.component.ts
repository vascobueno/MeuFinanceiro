import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { ButtonDirective } from 'primeng/button';
import { DatePicker } from 'primeng/datepicker';
import { Table } from 'primeng/table';
import { ProjectionService } from '../../core/services/projection.service';
import { Projection } from '../../core/models/projection.model';
import { ListStateComponent } from '../../shared/components/list-state.component';
import { StatCardComponent } from '../../shared/components/stat-card.component';

/** docs/11-projecao-financeira.md — permite consultar qualquer data futura. */
@Component({
  selector: 'mf-projection',
  standalone: true,
  imports: [CommonModule, FormsModule, MfIconComponent, ButtonDirective, DatePicker, Table, ListStateComponent, StatCardComponent],
  templateUrl: './projection.component.html',
  styleUrl: './projection.component.scss',
})
export class ProjectionComponent implements OnInit {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly projection = signal<Projection | null>(null);

  // Date (não string) por ser ligado a [(ngModel)] de mat-datepicker — ver comentário em
  // transaction-form.component.ts sobre o loop infinito de change detection que uma
  // conversão string->Date refeita a cada leitura provoca sob Angular zoneless.
  targetDate: Date = this.defaultTargetDate();

  constructor(private readonly projectionService: ProjectionService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.projectionService.getProjection(this.toIsoDate(this.targetDate)!).subscribe({
      next: (projection) => {
        this.projection.set(projection);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível calcular a projeção.');
        this.loading.set(false);
      },
    });
  }

  private defaultTargetDate(): Date {
    const date = new Date();
    date.setMonth(date.getMonth() + 3);
    return date;
  }

  private toIsoDate(value: Date | null): string | null {
    if (!value) return null;
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
