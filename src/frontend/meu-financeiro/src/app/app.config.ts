import { ApplicationConfig, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';

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
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
    provideAnimationsAsync(),
  ],
};
