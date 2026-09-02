import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule, MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../core/services/auth.service';
import { ProblemDetails } from '../../core/models/paged-result.model';
import { usernameValidators } from '../../shared/validators/username.validators';

/** Tela de login (RF001), espelhando docs/screens/login/mockup.html. */
@Component({
  selector: 'mf-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  providers: [{ provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { floatLabel: 'always', subscriptSizing: 'dynamic' } }],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showResendConfirmation = signal(false);
  readonly resendingConfirmation = signal(false);
  readonly resendConfirmationSent = signal(false);

  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    username: ['', usernameValidators],
    password: ['', [Validators.required, Validators.minLength(6)]],
  });

  readonly hidePassword = signal(true);

  constructor(
    private readonly authService: AuthService,
    private readonly router: Router,
  ) {}

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    this.showResendConfirmation.set(false);
    this.resendConfirmationSent.set(false);

    this.authService.login(this.form.getRawValue()).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/dashboard']);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        const problem = err.error as ProblemDetails | undefined;
        this.errorMessage.set(problem?.detail ?? 'Não foi possível entrar. Verifique suas credenciais.');
        this.showResendConfirmation.set(!!problem?.type?.endsWith('email-nao-confirmado'));
      },
    });
  }

  // Mesmo resultado em sucesso ou erro — o backend sempre responde 204 (evita enumeração de
  // contas), a UI segue o mesmo raciocínio.
  resendConfirmation(): void {
    const username = this.form.controls.username.value;
    if (!username) return;

    this.resendingConfirmation.set(true);
    this.authService.resendConfirmation({ username }).subscribe({
      next: () => {
        this.resendingConfirmation.set(false);
        this.resendConfirmationSent.set(true);
      },
      error: () => {
        this.resendingConfirmation.set(false);
        this.resendConfirmationSent.set(true);
      },
    });
  }
}
