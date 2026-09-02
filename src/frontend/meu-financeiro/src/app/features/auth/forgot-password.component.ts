import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule, MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../core/services/auth.service';
import { environment } from '../../../environments/environment';

/**
 * Pede o e-mail e chama forgot-password — o backend sempre responde 204, exista ou não a
 * conta (evita enumeração), então a tela sempre mostra a mesma mensagem genérica de sucesso.
 */
@Component({
  selector: 'mf-forgot-password',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  providers: [{ provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { floatLabel: 'always', subscriptSizing: 'dynamic' } }],
  templateUrl: './forgot-password.component.html',
  styleUrl: './forgot-password.component.scss',
})
export class ForgotPasswordComponent {
  readonly loading = signal(false);
  readonly submitted = signal(false);

  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  readonly turnstileSiteKey = environment.turnstileSiteKey;
  private captchaToken = '';

  constructor(private readonly authService: AuthService) {
    (window as unknown as Record<string, unknown>)['onForgotPasswordTurnstileSuccess'] = (token: string) => {
      this.captchaToken = token;
    };
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    const { email } = this.form.getRawValue();
    this.authService.forgotPassword({ email, captchaToken: this.captchaToken }).subscribe({
      next: () => {
        this.loading.set(false);
        this.submitted.set(true);
      },
      // Mesmo o backend só retornando 204, tratamos falha de rede/servidor mostrando a
      // mesma mensagem genérica — nunca revela se o e-mail existe ou não.
      error: () => {
        this.loading.set(false);
        this.submitted.set(true);
      },
    });
  }
}
