import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../core/services/auth.service';
import { ProblemDetails } from '../../core/models/paged-result.model';

/** Lê email/token da URL (do link recebido por e-mail) e confirma automaticamente ao abrir. */
@Component({
  selector: 'mf-confirm-email',
  standalone: true,
  imports: [CommonModule, RouterLink, MatCardModule, MatButtonModule, MatProgressSpinnerModule],
  templateUrl: './confirm-email.component.html',
  styleUrl: './confirm-email.component.scss',
})
export class ConfirmEmailComponent implements OnInit {
  readonly loading = signal(true);
  readonly success = signal(false);
  readonly errorMessage = signal<string | null>(null);

  private readonly email: string | null;
  private readonly token: string | null;
  readonly linkInvalido: boolean;

  constructor(
    private readonly authService: AuthService,
    route: ActivatedRoute,
  ) {
    this.email = route.snapshot.queryParamMap.get('email');
    this.token = route.snapshot.queryParamMap.get('token');
    this.linkInvalido = !this.email || !this.token;
  }

  ngOnInit(): void {
    if (this.linkInvalido) {
      this.loading.set(false);
      return;
    }

    this.authService.confirmEmail({ email: this.email!, token: this.token! }).subscribe({
      next: () => {
        this.loading.set(false);
        this.success.set(true);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        const problem = err.error as ProblemDetails | undefined;
        this.errorMessage.set(problem?.detail ?? 'Não foi possível confirmar seu e-mail.');
      },
    });
  }
}
