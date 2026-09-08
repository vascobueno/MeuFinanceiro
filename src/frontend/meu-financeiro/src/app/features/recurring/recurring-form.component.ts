import { Component, OnInit, signal } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MfIconComponent } from '../../shared/icons/mf-icon.component';
import { ButtonDirective } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { DatePicker } from 'primeng/datepicker';
import { ProgressSpinner } from 'primeng/progressspinner';
import { RecurringService } from '../../core/services/recurring.service';
import { RecurringTransaction } from '../../core/models/recurring.model';
import { RecurrenceFrequency } from '../../core/models/transaction.model';
import { MoneyInputComponent } from '../../shared/components/money-input.component';

/** Tela Recorrência, espelhando docs/screens/recorrencia/mockup.html. Regra 13. */
@Component({
  selector: 'mf-recurring-form',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MfIconComponent,
    ButtonDirective,
    InputText,
    Select,
    DatePicker,
    ProgressSpinner,
    MoneyInputComponent,
  ],
  templateUrl: './recurring-form.component.html',
  styleUrl: './recurring-form.component.scss',
})
export class RecurringFormComponent implements OnInit {
  readonly RecurrenceFrequency = RecurrenceFrequency;

  readonly frequencyOptions = [
    { label: 'Semanal', value: RecurrenceFrequency.Weekly },
    { label: 'Mensal', value: RecurrenceFrequency.Monthly },
    { label: 'Anual', value: RecurrenceFrequency.Yearly },
  ];

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly recurring = signal<RecurringTransaction | null>(null);

  newAmount: number | null = null;
  // Date (não string) por ser ligado a [(ngModel)] de p-datepicker — ver comentário
  // equivalente em transaction-form.component.ts sobre o loop infinito de change detection
  // que uma conversão string->Date refeita a cada leitura provoca sob Angular zoneless.
  effectiveFrom: Date | null = null;

  private id = '';

  constructor(
    private readonly route: ActivatedRoute,
    private readonly location: Location,
    private readonly recurringService: RecurringService,
  ) {}

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.recurringService.getById(this.id).subscribe({
      next: (recurring) => {
        this.recurring.set(recurring);
        this.newAmount = recurring.currentAmount;
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar a recorrência.');
        this.loading.set(false);
      },
    });
  }

  submit(): void {
    if (!this.effectiveFrom || this.newAmount === null || this.newAmount <= 0) {
      this.error.set('Informe o novo valor e a data de vigência.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.success.set(null);

    this.recurringService.createVersion(this.id, { effectiveFrom: this.toIsoDate(this.effectiveFrom)!, amount: this.newAmount }).subscribe({
      next: (recurring) => {
        this.recurring.set(recurring);
        this.saving.set(false);
        this.success.set('Nova versão salva. Ocorrências anteriores e conciliadas permanecem inalteradas.');
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.detail ?? 'Não foi possível salvar a nova versão.');
      },
    });
  }

  back(): void {
    this.location.back();
  }

  private toIsoDate(value: Date | null): string | null {
    if (!value) return null;
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
