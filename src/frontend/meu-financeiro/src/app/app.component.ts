import { Component, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { InputText } from 'primeng/inputtext';
import { ThemeService } from './core/services/theme.service';
import { IntroAnimationService } from './core/services/intro-animation.service';
import { IntroLogoComponent } from './shared/components/intro-logo.component';

@Component({
  selector: 'mf-root',
  standalone: true,
  imports: [RouterOutlet, InputText, IntroLogoComponent],
  // Input oculto só pra forçar o registro das variáveis CSS do InputText (--p-inputtext-*)
  // logo na raiz: o PrimeNG carrega o CSS de cada componente sob demanda, na 1ª instância
  // usada (BaseComponent._loadThemeStyles) — o CSS do PasswordDirective (pPassword) referencia
  // essas mesmas variáveis mas nunca registra o bloco sozinho. Nas telas de auth com um campo
  // de e-mail (login/register) isso "funciona" de carona; em telas só-com-senha
  // (reset-password/change-password) as variáveis ficavam undefined e a borda do campo
  // desaparecia (var() inválida faz o border cair pro valor inicial: style none → 0px).
  template: `
    <input pInputText type="text" tabindex="-1" aria-hidden="true" class="mf-sr-only" />
    @if (showSplash()) {
      <div
        class="mf-splash-overlay"
        [class.mf-splash-closing]="splashClosing()"
        (click)="skipSplash()"
        (keydown)="skipSplash()"
      >
        <mf-intro-logo class="mf-splash-logo" (done)="skipSplash()" />
      </div>
    }
    <router-outlet />
  `,
  styles: `
    .mf-sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }
    .mf-splash-overlay {
      position: fixed;
      inset: 0;
      z-index: 9999;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--mf-bg, #f7f4ef);
      opacity: 1;
      transition: opacity 0.25s ease-in;
    }
    .mf-splash-overlay.mf-splash-closing {
      opacity: 0;
    }
    .mf-splash-logo {
      height: 260px;
    }
  `,
})
export class AppComponent {
  // Injetado aqui (raiz de tudo, antes do Login ou do Shell) pra já nascer com o skin salvo
  // — ThemeService só aplica data-theme quando alguém o injeta (providedIn: 'root' é lazy).
  private readonly themeService = inject(ThemeService);
  private readonly introService = inject(IntroAnimationService);

  // router-outlet fica sempre montado por baixo (guards resolvem a rota em paralelo); o
  // overlay só cobre visualmente até a animação acabar (done) ou até um clique/tecla.
  readonly showSplash = signal(this.introService.mode() === 'global' && !this.introService.hasPlayed());
  readonly splashClosing = signal(false);

  skipSplash(): void {
    if (this.splashClosing()) return;
    this.splashClosing.set(true);
    setTimeout(() => this.showSplash.set(false), 250);
  }
}
