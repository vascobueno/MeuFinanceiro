import { Component, EventEmitter, Output, ViewEncapsulation, inject, signal } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { IntroAnimationService } from '../../core/services/intro-animation.service';

// Precisa bater com a soma delay+duração da última etapa (.mf-p-wm-tagline) no .scss.
const TOTAL_ANIMATION_MS = 2150;

/**
 * Logo animada de entrada: injeta o SVG classificado por peça (logo.svg — carteira, dinheiro,
 * moeda, seta, anel, barras e as partes do wordmark, cada uma com uma classe `mf-p-*`) direto
 * no DOM via innerHTML, pra animar os objetos de verdade via CSS (@keyframes no .scss deste
 * componente) — precisa estar no DOM real porque animação CSS não avança de forma confiável
 * dentro de um SVG carregado só como `<img src>`. Usada na tela de login e no splash global
 * opcional (IntroAnimationService decide o modo). Toca de novo a cada carregamento de página,
 * mas nunca quando prefers-reduced-motion está ativo nem mais de uma vez dentro da mesma
 * página já carregada — fora dessas condições, renderiza o SVG estático, sem nenhuma classe
 * de animação aplicada.
 */
@Component({
  selector: 'mf-intro-logo',
  standalone: true,
  encapsulation: ViewEncapsulation.None,
  templateUrl: './intro-logo.component.html',
  styleUrl: './intro-logo.component.scss',
})
export class IntroLogoComponent {
  @Output() readonly done = new EventEmitter<void>();

  private readonly introService = inject(IntroAnimationService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly shouldAnimate =
    this.introService.mode() !== 'off' &&
    !this.introService.hasPlayed() &&
    !(typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches);

  readonly svgContent = signal<SafeHtml | null>(null);

  constructor() {
    // Marca "já visto" assim que decide animar, não só quando a animação termina — um refresh
    // no meio da animação não pode fazer ela rodar de novo pra sempre na próxima carga.
    if (this.shouldAnimate) {
      this.introService.markPlayed();
    }

    fetch('/logo.svg')
      .then((res) => res.text())
      .then((text) => {
        this.svgContent.set(this.sanitizer.bypassSecurityTrustHtml(text));
        if (this.shouldAnimate) {
          setTimeout(() => this.done.emit(), TOTAL_ANIMATION_MS);
        }
      });
  }
}
