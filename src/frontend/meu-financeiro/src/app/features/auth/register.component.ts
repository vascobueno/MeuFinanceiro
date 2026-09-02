import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
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
import { environment } from '../../../environments/environment';

/**
 * Cadastro de novo usuário (RF001) — cria a identidade e, na mesma chamada, o grupo novo do
 * usuário. Não loga mais automaticamente — precisa confirmar o e-mail primeiro
 * (docs/12-seguranca.md), então só mostra uma mensagem de sucesso em vez de navegar.
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
  readonly submitted = signal(false);

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

  readonly turnstileSiteKey = environment.turnstileSiteKey;
  private captchaToken = '';

  constructor(private readonly authService: AuthService) {
    // Callback do widget Turnstile (index.html carrega o script; o widget é renderizado via
    // [attr.data-sitekey] no template, só quando turnstileSiteKey está configurado).
    (window as unknown as Record<string, unknown>)['onRegisterTurnstileSuccess'] = (token: string) => {
      this.captchaToken = token;
    };
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    const { name, username, email, password } = this.form.getRawValue();
    this.authService.register({ name, username, email, password, captchaToken: this.captchaToken }).subscribe({
      next: () => {
        this.loading.set(false);
        this.submitted.set(true);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        const problem = err.error as ProblemDetails | undefined;
        this.errorMessage.set(problem?.detail ?? 'Não foi possível criar sua conta. Tente novamente.');
      },
    });
  }
}
