import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule, MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../core/services/auth.service';
import { ProblemDetails } from '../../core/models/paged-result.model';
import { passwordValidators, passwordsMatchValidator } from '../../shared/validators/password.validators';
import { usernameValidators } from '../../shared/validators/username.validators';

/**
 * Cadastro de novo usuário (RF001) — cria a identidade e, na mesma chamada, o grupo
 * novo do usuário (AuthService.register já loga automaticamente, igual ao login).
 */
@Component({
  selector: 'mf-register',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  providers: [{ provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { floatLabel: 'always', subscriptSizing: 'dynamic' } }],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
})
export class RegisterComponent {
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group(
    {
      name: ['', [Validators.required, Validators.minLength(2)]],
      username: ['', usernameValidators],
      email: ['', [Validators.required, Validators.email]],
      password: ['', passwordValidators],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: [passwordsMatchValidator] },
  );

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

    const { name, username, email, password } = this.form.getRawValue();
    this.authService.register({ name, username, email, password }).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/dashboard']);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        const problem = err.error as ProblemDetails | undefined;
        this.errorMessage.set(problem?.detail ?? 'Não foi possível criar sua conta. Tente novamente.');
      },
    });
  }
}
