import {
  ApplicationConfig,
  DEFAULT_CURRENCY_CODE,
  LOCALE_ID,
  inject,
  provideAppInitializer,
  provideZonelessChangeDetection,
} from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localePt from '@angular/common/locales/pt';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { providePrimeNG } from 'primeng/config';
import { firstValueFrom } from 'rxjs';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { AuthService } from './core/services/auth.service';
import { environment } from '../environments/environment';
import { MfPreset } from './styles/primeng-preset';
import { primeNgTranslationPtBr } from './shared/i18n/primeng-translation-pt-br';

// Sem isto os pipes currency/number/date formatavam em en-US em todo o app ("R$1,234.56"); só o
// Dashboard escapava, por usar toLocaleString('pt-BR') direto.
registerLocaleData(localePt, 'pt-BR');

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
    { provide: LOCALE_ID, useValue: 'pt-BR' },
    { provide: DEFAULT_CURRENCY_CODE, useValue: 'BRL' },
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([authInterceptor, errorInterceptor]),
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
    ),
    provideAnimationsAsync(),
    // PrimeNG (docs/migracao-primeng/00-plano.md) — coexiste com o Material durante a
    // migração. MfPreset (styles/primeng-preset.ts) mapeia a cor "primary" pros tokens
    // --mf-* já existentes, então a troca de skin (ThemeService) também vale pro PrimeNG.
    // Sem license key configurada, PrimeNG só mostra um aviso no console — não bloqueia
    // nada (mesmo padrão de degradação graciosa do SMTP/Turnstile).
    providePrimeNG({
      theme: { preset: MfPreset },
      license: environment.primeNgLicenseKey,
      translation: primeNgTranslationPtBr,
    }),
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
