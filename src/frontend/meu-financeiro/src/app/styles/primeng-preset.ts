import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';

/**
 * UI-00.4 (docs/migracao-primeng/00-plano.md) — mapeia a cor "primary" do PrimeNG pras
 * mesmas variáveis CSS --mf-* já usadas pelo Material (src/styles/_tokens.scss), em vez de
 * valores fixos. Como a troca de skin (ThemeService, atributo data-theme no <html>) só
 * sobrescreve essas custom properties, os componentes PrimeNG acompanham a skin ativa
 * automaticamente pela cascata do CSS — sem precisar recriar o preset a cada troca de tema.
 *
 * Os 7 skins hoje só variam --mf-primary/--mf-primary-dark/--mf-primary-tint (superfícies e
 * texto são compartilhados por todos) — por isso só a cor "primary" é remapeada aqui; as
 * demais (surface, texto) ficam no padrão do preset Aura até serem revalidadas visualmente
 * nas Fases 1+ (ver "Riscos" no plano).
 */
export const MfPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: 'var(--mf-primary-tint)',
      100: 'var(--mf-primary-tint)',
      200: 'var(--mf-primary-tint)',
      300: 'var(--mf-primary)',
      400: 'var(--mf-primary)',
      500: 'var(--mf-primary)',
      600: 'var(--mf-primary)',
      700: 'var(--mf-primary-dark)',
      800: 'var(--mf-primary-dark)',
      900: 'var(--mf-primary-dark)',
      950: 'var(--mf-primary-dark)',
      color: 'var(--mf-primary)',
      contrastColor: '#ffffff',
      hoverColor: 'var(--mf-primary-dark)',
      activeColor: 'var(--mf-primary-dark)',
    },
  },
});
