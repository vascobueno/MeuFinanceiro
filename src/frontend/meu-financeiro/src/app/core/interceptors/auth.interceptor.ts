import { HttpInterceptorFn } from '@angular/common/http';
import { environment } from '../../../environments/environment';

/**
 * Marca as chamadas pra própria API com withCredentials — necessário pro navegador enviar
 * (e aceitar) os cookies httpOnly de sessão em requisições cross-origin (front e API em
 * portas/domínios diferentes). O token em si nunca passa por aqui — vai sozinho no cookie
 * (docs/12-seguranca.md).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.apiUrl)) return next(req);

  return next(req.clone({ withCredentials: true }));
};
