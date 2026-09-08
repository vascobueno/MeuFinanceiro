import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { PasswordDirective } from 'primeng/password';
import { ButtonDirective } from 'primeng/button';
import { ProgressSpinner } from 'primeng/progressspinner';
import { AuthService } from '../../core/services/auth.service';
import { ProblemDetails } from '../../core/models/paged-result.model';
import { passwordValidators, passwordsMatchValidator } from '../../shared/validators/password.validators';

/** Lê email/token da URL (do link recebido por e-mail) e define uma nova senha. */
@Component({
  selector: 'mf-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PasswordDirective, ButtonDirective, ProgressSpinner],
  templateUrl: './reset-password.component.html',
  styleUrl: './reset-password.component.scss',
})
export class ResetPasswordComponent {
  readonly loading = signal(false);
  readonly submitted = signal(false);
  readonly errorMessage = signal<string | null>(null);

  private readonly fb = inject(FormBuilder);
  readonly email: string | null;
  private readonly token: string | null;

  readonly linkInvalido: boolean;

  readonly form = this.fb.nonNullable.group(
    {
      password: ['', passwordValidators],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: [passwordsMatchValidator] },
  );

  readonly hidePassword = signal(true);
  readonly hideConfirmPassword = signal(true);

  constructor(
    private readonly authService: AuthService,
    route: ActivatedRoute,
  ) {
    this.email = route.snapshot.queryParamMap.get('email');
    this.token = route.snapshot.queryParamMap.get('token');
    this.linkInvalido = !this.email || !this.token;
  }

  submit(): void {
    if (this.form.invalid || !this.email || !this.token) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    const { password } = this.form.getRawValue();
    this.authService.resetPassword({ email: this.email, token: this.token, newPassword: password }).subscribe({
      next: () => {
        this.loading.set(false);
        this.submitted.set(true);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        const problem = err.error as ProblemDetails | undefined;
        this.errorMessage.set(problem?.detail ?? 'Não foi possível redefinir sua senha. Tente novamente.');
      },
    });
  }
}
