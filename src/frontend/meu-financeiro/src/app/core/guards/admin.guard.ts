import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/// Só a UI é escondida/redirecionada aqui — o backend já rejeita com 403 independente disso
/// ([Authorize(Roles = "Administrator")] no AdminController).
export const adminGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.currentUser()?.isAdministrator) return true;

  router.navigate(['/dashboard']);
  return false;
};
