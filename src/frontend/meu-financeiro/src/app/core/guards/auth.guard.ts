import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  // Não tem token pra checar em JS (cookie httpOnly) — currentUser() já foi (tentado a)
  // populado pelo initializer do bootstrap (app.config.ts) via GET /auth/me.
  if (!authService.currentUser()) {
    router.navigate(['/login']);
    return false;
  }

  if (authService.currentUser()?.mustChangePassword && state.url !== '/trocar-senha-inicial') {
    router.navigate(['/trocar-senha-inicial']);
    return false;
  }

  return true;
};
