import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Subject, catchError, switchMap, take, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

// Módulo-level (não por-injeção): precisa ser compartilhado entre todas as chamadas que
// caírem em 401 ao mesmo tempo, pra enfileirar e disparar só UM refresh em paralelo, em vez
// de um por requisição.
let refreshInProgress = false;
const refreshed$ = new Subject<void>();

/**
 * Trata 401 globalmente: tenta UM refresh (o cookie de refresh vai sozinho) e repete a
 * requisição original — só desloga se o próprio refresh também falhar. Antes da Fase 7
 * (cookie httpOnly), isso não existia e a sessão morria a cada expiração do access token;
 * agora o usuário nem percebe (docs/12-seguranca.md).
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: unknown) => {
      const isAuthRoute = req.url.includes('/auth/login') || req.url.includes('/auth/refresh') || req.url.includes('/auth/me');

      if (error instanceof HttpErrorResponse && error.status === 401 && !isAuthRoute) {
        if (!refreshInProgress) {
          refreshInProgress = true;
          authService.refresh().subscribe({
            next: () => {
              refreshInProgress = false;
              refreshed$.next();
            },
            error: () => {
              refreshInProgress = false;
              authService.logout();
              router.navigate(['/login']);
            },
          });
        }

        return refreshed$.pipe(
          take(1),
          switchMap(() => next(req)),
        );
      }

      return throwError(() => error);
    }),
  );
};
