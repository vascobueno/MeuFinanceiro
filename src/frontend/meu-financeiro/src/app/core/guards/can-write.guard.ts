import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/// Guarda rotas de formulário (criar/editar) contra navegação direta por URL de um Member
/// Visualizador. Só a UI é escondida/redirecionada aqui — o backend já rejeita com 403
/// independente disso (WriteAccessFilter).
export const canWriteGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.canWrite()) return true;

  router.navigate(['/dashboard']);
  return false;
};
