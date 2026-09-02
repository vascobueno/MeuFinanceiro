import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/// Guarda as páginas de gestão do grupo (Configurações/Membros) — Member com perfil
/// Editor/Visualizador não vê essas páginas. Só a UI é escondida/redirecionada aqui — o
/// backend já rejeita com 403 independente disso (GruposController.GetMine).
export const fullAccessGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.hasFullAccess()) return true;

  router.navigate(['/dashboard']);
  return false;
};
