import { Injectable, effect, signal } from '@angular/core';

export type Skin = 'laranja' | 'azul' | 'verde' | 'roxo' | 'cinza' | 'preto' | 'amarelo';

const STORAGE_KEY = 'mf-theme';

// Mesmos hex documentados no plano (Parte 6) — usados só pro círculo do seletor;
// a cor "de verdade" na tela vem do CSS (--mf-primary por [data-theme]).
const SWATCH_COLORS: Record<Skin, string> = {
  laranja: '#df5830',
  azul: '#2f6fed',
  verde: '#1f9d6b',
  roxo: '#7c4dff',
  cinza: '#6b6a63',
  preto: '#252220',
  amarelo: '#c99a0e',
};

// Cinza e Amarelo são claros demais pro check branco de dentro do círculo ficar legível.
const LIGHT_SKINS = new Set<Skin>(['cinza', 'amarelo']);

/**
 * Skin de cor do app (Parte 6 do plano) — só troca o acento (--mf-primary/-dark/-tint) via
 * atributo data-theme no <html>; fundo/cartões/textos continuam iguais nos 7. Persistido em
 * localStorage, mesmo padrão já usado pro mf-sidebar-collapsed em shell.component.ts.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly skins: Skin[] = ['laranja', 'azul', 'verde', 'roxo', 'cinza', 'preto', 'amarelo'];

  readonly theme = signal<Skin>((localStorage.getItem(STORAGE_KEY) as Skin | null) ?? 'laranja');

  constructor() {
    effect(() => {
      const value = this.theme();
      document.documentElement.setAttribute('data-theme', value);
      localStorage.setItem(STORAGE_KEY, value);
    });
  }

  swatchColor(skin: Skin): string {
    return SWATCH_COLORS[skin];
  }

  swatchCheckColor(skin: Skin): string {
    return LIGHT_SKINS.has(skin) ? '#2b2621' : '#ffffff';
  }
}
