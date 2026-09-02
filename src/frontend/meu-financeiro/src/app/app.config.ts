import { ApplicationConfig, inject, provideAppInitializer, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { firstValueFrom } from 'rxjs';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { AuthService } from './core/services/auth.service';

/**
 * Zoneless (docs/14-angular.md): sem zone.js, o Angular só verifica um componente quando
 * algo o marca como "sujo" — leitura de signal usada no template mudando, evento de
 * template (click, ngModelChange, output de filho), ou ChangeDetectorRef.markForCheck().
 * Todo o app foi auditado componente a componente para garantir que só depende desses três
 * gatilhos (ver AutocompleteComponent e MoneyInputComponent, que precisaram de
 * markForCheck() explícito por atualizarem estado fora de um evento de template).
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideZonelessChangeDetection(),
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([authInterceptor, errorInterceptor]),
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
    ),
    provideAnimationsAsync(),
    // Busca o cookie CSRF e tenta rehidratar a sessão a partir do cookie httpOnly (sobrevive
    // a um F5, já que não há mais token nenhum em localStorage) antes de renderizar as rotas
    // — 401 aqui é o caso normal de "ainda não logado", não um erro de bootstrap.
    provideAppInitializer(async () => {
      const authService = inject(AuthService);
      try {
        await firstValueFrom(authService.fetchCsrfToken());
        await firstValueFrom(authService.me());
      } catch {
        // Não autenticado — segue normalmente, authGuard cuida do redirecionamento.
      }
    }),
  ],
};
