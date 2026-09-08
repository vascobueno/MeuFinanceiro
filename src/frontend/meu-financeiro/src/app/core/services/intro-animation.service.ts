import { Injectable, effect, signal } from '@angular/core';

export type IntroMode = 'login' | 'global' | 'off';

const MODE_KEY = 'mf-intro-mode';

/**
 * Onde a animação de entrada da logo (IntroLogoComponent) roda: só na tela de login, num
 * splash em tela cheia ao abrir o site, ou desativada. Mesmo padrão do ThemeService pro `mode`
 * (signal + effect persistindo em localStorage). `hasPlayed` já nasce zerado em cada carga da
 * página (sem persistência) — a animação toca de novo toda vez que o site é aberto ou a tela
 * recarregada; só não repete entre navegações internas (SPA) dentro da mesma página carregada,
 * porque o service é um singleton e `markPlayed()` marca assim que decide animar.
 */
@Injectable({ providedIn: 'root' })
export class IntroAnimationService {
  readonly mode = signal<IntroMode>((localStorage.getItem(MODE_KEY) as IntroMode | null) ?? 'login');
  readonly hasPlayed = signal(false);

  constructor() {
    effect(() => localStorage.setItem(MODE_KEY, this.mode()));
  }

  markPlayed(): void {
    this.hasPlayed.set(true);
  }
}
