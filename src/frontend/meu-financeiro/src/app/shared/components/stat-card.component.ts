import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Card de indicador (KPI) do design V4 (docs/screens/<tela>/mockup.html): rótulo + valor em
 * destaque + texto pequeno opcional (positivo/negativo). variant="hero" usa o card com
 * gradiente reservado ao indicador principal da tela (ex.: patrimônio consolidado).
 * O visual é 100% customizado (nunca usou nenhum recurso específico do mat-card além do
 * container) — migrado direto pra uma div própria, sem trazer nenhuma lib de UI
 * (docs/migracao-primeng/00-plano.md, UI-01.2).
 *
 * size="compact" põe rótulo e valor na MESMA linha, com altura equivalente à de um
 * p-select size="small" (~34px). Usado onde o KPI acompanha um seletor e não pode dominar a
 * tela — as duas de Lançamentos. O padrão fica intacto nos demais usos.
 */
@Component({
  selector: 'mf-stat-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div
      class="mf-stat-card"
      [class.mf-stat-card-hero]="variant === 'hero'"
      [class.mf-stat-card-compact]="size === 'compact'"
      [title]="size === 'compact' && hint ? hint : null"
    >
      <div class="mf-stat-label">{{ label }}</div>
      <div class="mf-stat-value" [ngClass]="size === 'compact' ? hintClass : ''">{{ value }}</div>
      @if (hint && size !== 'compact') {
        <div class="mf-stat-small" [ngClass]="hintClass">{{ hint }}</div>
      }
    </div>
  `,
  styles: [
    `
      .mf-stat-card {
        display: block;
        height: 100%;
        padding: 1rem;
        background: var(--mf-card, #fff);
        border: 1px solid var(--mf-line, #e8e1d8);
        border-radius: var(--mf-radius, 0.75rem);
        box-shadow: var(--mf-shadow, 0 1px 2px rgba(43, 38, 33, 0.04), 0 10px 24px -16px rgba(43, 38, 33, 0.18));
      }
      .mf-stat-card-hero {
        background: linear-gradient(135deg, #8f3814, var(--mf-primary, #df5830) 60%, #f0855a);
        color: #fff;
        border: none;
        box-shadow: 0 14px 30px rgba(223, 88, 48, 0.2);
      }
      .mf-stat-card-hero .mf-stat-label,
      .mf-stat-card-hero .mf-stat-small {
        color: #ffe2d4;
      }
      .mf-stat-label {
        font-size: 0.65rem;
        color: var(--mf-muted, #8b8175);
        text-transform: uppercase;
        font-weight: 800;
        letter-spacing: 0.04em;
      }
      .mf-stat-value {
        font-size: 1.5rem;
        font-weight: 800;
        margin: 0.4rem 0;
      }
      .mf-stat-small {
        font-size: 0.65rem;
        color: var(--mf-muted, #8b8175);
      }
      .mf-stat-small.positive {
        color: var(--mf-green, #12966f);
      }
      .mf-stat-small.negative {
        color: var(--mf-red, #c0392b);
      }

      /* Uma linha só: o hint vira title (tooltip nativo) — é o que tira a altura. */
      .mf-stat-card-compact {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 0.75rem;
        padding: 0.25rem 0.75rem;
      }
      .mf-stat-card-compact .mf-stat-value {
        font-size: 1rem;
        margin: 0;
      }
      /* No compacto o tom vai para o VALOR, já que o texto pequeno não é exibido. */
      .mf-stat-card-compact .mf-stat-value.positive {
        color: var(--mf-green, #12966f);
      }
      .mf-stat-card-compact .mf-stat-value.negative {
        color: var(--mf-red, #c0392b);
      }
      .mf-stat-card-compact.mf-stat-card-hero .mf-stat-value.positive,
      .mf-stat-card-compact.mf-stat-card-hero .mf-stat-value.negative {
        color: #fff;
      }
    `,
  ],
})
export class StatCardComponent {
  @Input({ required: true }) label!: string;
  @Input({ required: true }) value!: string | null;
  @Input() hint?: string | null;
  @Input() hintTone: 'positive' | 'negative' | 'neutral' = 'neutral';
  @Input() variant: 'default' | 'hero' = 'default';
  /** 'compact' = rótulo e valor na mesma linha, na altura de um p-select pequeno. */
  @Input() size: 'default' | 'compact' = 'default';

  get hintClass(): string {
    return this.hintTone === 'neutral' ? '' : this.hintTone;
  }
}
