import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { PasswordDirective } from 'primeng/password';
import { ButtonDirective } from 'primeng/button';
import { ProgressSpinner } from 'primeng/progressspinner';
import { AuthService } from '../../core/services/auth.service';
import { ProblemDetails } from '../../core/models/paged-result.model';
import { passwordValidators, passwordsMatchValidator } from '../../shared/validators/password.validators';

/**
 * Troca obrigatória de senha temporária (User.MustChangePassword) — o authGuard redireciona
 * pra cá antes de qualquer outra tela até essa troca acontecer.
 */
@Component({
  selector: 'mf-change-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PasswordDirective, ButtonDirective, ProgressSpinner],
  templateUrl: './change-password.component.html',
  styleUrl: './change-password.component.scss',
})
export class ChangePasswordComponent {
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group(
    {
      currentPassword: ['', [Validators.required]],
      password: ['', passwordValidators],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: [passwordsMatchValidator] },
  );

  readonly hideCurrentPassword = signal(true);
  readonly hidePassword = signal(true);
  readonly hideConfirmPassword = signal(true);

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

    const { currentPassword, password } = this.form.getRawValue();
    this.authService.changePassword({ currentPassword, newPassword: password }).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/dashboard']);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        const problem = err.error as ProblemDetails | undefined;
        this.errorMessage.set(problem?.detail ?? 'Não foi possível trocar sua senha. Tente novamente.');
      },
    });
  }
}
