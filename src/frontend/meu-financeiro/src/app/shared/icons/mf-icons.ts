/**
 * Ícones flat (formas sólidas em currentColor, sem contorno) inspirados no estilo de sets de
 * "ícones de negócios simples" pedido pelo usuário — usados no menu lateral e nos títulos de
 * página. currentColor faz cada um herdar a cor do elemento que aplica a classe (`mf-icon`),
 * sem precisar de CSS por ícone.
 *
 * Migrado do registro `MatIconRegistry` (Fase 6, UI-06.2) pro componente `MfIconComponent` —
 * `width`/`height` explícitos aqui (24x24) são só o tamanho intrínseco do SVG; quem realmente
 * controla o tamanho renderizado é a regra `:host ::ng-deep svg { width: 100%; height: 100% }`
 * de `mf-icon.component.ts`, que ignora esses atributos e deixa o tamanho do host (via classes
 * como `.mf-page-title-icon`/`.mf-nav-icon`) mandar — mesmo comportamento que o Material tinha
 * internamente pra `[svgIcon]`.
 */
export const MF_ICONS: Record<string, string> = {
  'mf-dashboard': `<svg viewBox="0 0 24 24" width="24" height="24">
    <rect x="3" y="3" width="10" height="8" rx="2" fill="currentColor"/>
    <rect x="15" y="3" width="6" height="5" rx="2" fill="currentColor" opacity="0.55"/>
    <rect x="15" y="10" width="6" height="11" rx="2" fill="currentColor" opacity="0.75"/>
    <rect x="3" y="13" width="10" height="8" rx="2" fill="currentColor" opacity="0.4"/>
  </svg>`,

  // Duas setas retas (não curvas com arco de ponta) pra controlar a extensão vertical de cada
  // uma com precisão — evita a ponta de uma encostar na outra como acontecia com o path anterior.
  'mf-transactions': `<svg viewBox="0 0 24 24" width="24" height="24">
    <path d="M3 6h11V3.9L21 7l-7 3.1V8H3z" fill="currentColor"/>
    <path d="M21 15H10v-2.1L3 16l7 3.1V17h11z" fill="currentColor" opacity="0.6"/>
  </svg>`,

  'mf-recurring': `<svg viewBox="0 0 24 24" width="24" height="24">
    <path d="M12 4a8 8 0 00-7.75 6 1 1 0 001.94.5A6 6 0 0112 6a6 6 0 015.65 4H15a1 1 0 000 2h5a1 1 0 001-1V6a1 1 0 10-2 0v2.35A8 8 0 0012 4z" fill="currentColor"/>
    <path d="M12 20a8 8 0 007.75-6 1 1 0 00-1.94-.5A6 6 0 0112 18a6 6 0 01-5.65-4H9a1 1 0 000-2H4a1 1 0 00-1 1v5a1 1 0 102 0v-2.35A8 8 0 0012 20z" fill="currentColor" opacity="0.6"/>
  </svg>`,

  'mf-installments': `<svg viewBox="0 0 24 24" width="24" height="24">
    <rect x="3" y="4" width="18" height="4.5" rx="1.5" fill="currentColor"/>
    <rect x="3" y="10" width="18" height="4.5" rx="1.5" fill="currentColor" opacity="0.65"/>
    <rect x="3" y="16" width="18" height="4.5" rx="1.5" fill="currentColor" opacity="0.4"/>
  </svg>`,

  'mf-accounts': `<svg viewBox="0 0 24 24" width="24" height="24">
    <path d="M12 2l10 5.5a1 1 0 01-.5 1.9H2.5a1 1 0 01-.5-1.9L12 2z" fill="currentColor"/>
    <rect x="4" y="10" width="3" height="8" fill="currentColor" opacity="0.65"/>
    <rect x="10.5" y="10" width="3" height="8" fill="currentColor" opacity="0.65"/>
    <rect x="17" y="10" width="3" height="8" fill="currentColor" opacity="0.65"/>
    <rect x="2" y="19" width="20" height="2.5" rx="1" fill="currentColor"/>
  </svg>`,

  'mf-cards': `<svg viewBox="0 0 24 24" width="24" height="24">
    <rect x="2" y="5" width="20" height="14" rx="2.5" fill="currentColor" opacity="0.35"/>
    <rect x="2" y="8" width="20" height="3.2" fill="currentColor"/>
    <rect x="4.5" y="14.5" width="6" height="2" rx="1" fill="currentColor"/>
  </svg>`,

  'mf-invoices': `<svg viewBox="0 0 24 24" width="24" height="24">
    <path d="M5 2h14a1 1 0 011 1v18l-2.5-1.5L15 21l-2.5-1.5L10 21l-2.5-1.5L5 21V3a1 1 0 011-1z" fill="currentColor" opacity="0.35"/>
    <rect x="7.5" y="6" width="9" height="1.8" rx="0.9" fill="currentColor"/>
    <rect x="7.5" y="10" width="9" height="1.8" rx="0.9" fill="currentColor"/>
    <rect x="7.5" y="14" width="6" height="1.8" rx="0.9" fill="currentColor"/>
  </svg>`,

  'mf-reconciliation-account': `<svg viewBox="0 0 24 24" width="24" height="24">
    <rect x="4" y="3" width="16" height="19" rx="2" fill="currentColor" opacity="0.35"/>
    <rect x="8" y="1.5" width="8" height="3.5" rx="1.5" fill="currentColor"/>
    <path d="M8 13.5l2.3 2.3L16.5 9.6" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/>
  </svg>`,

  'mf-reconciliation-card': `<svg viewBox="0 0 24 24" width="24" height="24">
    <rect x="2" y="5" width="20" height="14" rx="2.5" fill="currentColor" opacity="0.35"/>
    <rect x="2" y="8" width="20" height="3.2" fill="currentColor"/>
    <path d="M9 15.2l2 2 4.5-4.5" fill="none" stroke="currentColor" stroke-width="2.1" stroke-linecap="round" stroke-linejoin="round"/>
  </svg>`,

  'mf-investments': `<svg viewBox="0 0 24 24" width="24" height="24">
    <rect x="3" y="13" width="3.2" height="8" rx="1" fill="currentColor" opacity="0.45"/>
    <rect x="8" y="9" width="3.2" height="12" rx="1" fill="currentColor" opacity="0.7"/>
    <rect x="13" y="5" width="3.2" height="16" rx="1" fill="currentColor" opacity="0.9"/>
    <path d="M17 4h4a1 1 0 011 1v4a1 1 0 11-2 0V7.4l-3.3 3.3a1 1 0 01-1.4-1.4L18.6 6H17a1 1 0 110-2z" fill="currentColor"/>
  </svg>`,

  'mf-projection': `<svg viewBox="0 0 24 24" width="24" height="24">
    <rect x="3" y="12" width="3.2" height="9" rx="1" fill="currentColor" opacity="0.45"/>
    <rect x="8" y="8" width="3.2" height="13" rx="1" fill="currentColor" opacity="0.7"/>
    <circle cx="17.5" cy="7.5" r="4.2" fill="none" stroke="currentColor" stroke-width="1.8"/>
    <circle cx="17.5" cy="7.5" r="1.6" fill="currentColor"/>
  </svg>`,

  // Dentes (8 retângulos radiais) + anel com furo real no centro (dois círculos no mesmo
  // path com fill-rule="evenodd" — a região onde os dois se sobrepõem vira o furo).
  'mf-settings': `<svg viewBox="0 0 24 24" width="24" height="24">
    <rect x="10.7" y="1.8" width="2.6" height="4.8" rx="1.2" fill="currentColor" transform="rotate(0 12 12)"/>
    <rect x="10.7" y="1.8" width="2.6" height="4.8" rx="1.2" fill="currentColor" transform="rotate(45 12 12)"/>
    <rect x="10.7" y="1.8" width="2.6" height="4.8" rx="1.2" fill="currentColor" transform="rotate(90 12 12)"/>
    <rect x="10.7" y="1.8" width="2.6" height="4.8" rx="1.2" fill="currentColor" transform="rotate(135 12 12)"/>
    <rect x="10.7" y="1.8" width="2.6" height="4.8" rx="1.2" fill="currentColor" transform="rotate(180 12 12)"/>
    <rect x="10.7" y="1.8" width="2.6" height="4.8" rx="1.2" fill="currentColor" transform="rotate(225 12 12)"/>
    <rect x="10.7" y="1.8" width="2.6" height="4.8" rx="1.2" fill="currentColor" transform="rotate(270 12 12)"/>
    <rect x="10.7" y="1.8" width="2.6" height="4.8" rx="1.2" fill="currentColor" transform="rotate(315 12 12)"/>
    <path fill-rule="evenodd" fill="currentColor" d="M12 6a6 6 0 100 12 6 6 0 000-12zm0 3.3a2.7 2.7 0 100 5.4 2.7 2.7 0 000-5.4z"/>
  </svg>`,

  'mf-admin': `<svg viewBox="0 0 24 24" width="24" height="24">
    <path d="M12 2l8 3v6c0 5-3.5 9-8 11-4.5-2-8-6-8-11V5z" fill="currentColor" opacity="0.35"/>
    <path d="M8.3 12.3l2.4 2.4L16 9.2" fill="none" stroke="currentColor" stroke-width="2.1" stroke-linecap="round" stroke-linejoin="round"/>
  </svg>`,

  'mf-audit': `<svg viewBox="0 0 24 24" width="24" height="24">
    <rect x="3.5" y="2" width="13" height="17" rx="2" fill="currentColor" opacity="0.35"/>
    <rect x="6" y="5.5" width="8" height="1.7" rx="0.85" fill="currentColor"/>
    <rect x="6" y="9.3" width="8" height="1.7" rx="0.85" fill="currentColor"/>
    <circle cx="16.8" cy="16.8" r="4" fill="none" stroke="currentColor" stroke-width="2"/>
    <line x1="19.6" y1="19.6" x2="21.8" y2="21.8" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
  </svg>`,

  'mf-help': `<svg viewBox="0 0 24 24" width="24" height="24">
    <circle cx="12" cy="12" r="10" fill="currentColor" opacity="0.35"/>
    <path d="M9.3 9.6a2.7 2.7 0 015.2-1 2.4 2.4 0 01-1.1 3c-.8.5-1.1.9-1.1 1.7v.4" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>
    <circle cx="12.2" cy="17" r="1.15" fill="currentColor"/>
  </svg>`,
};
