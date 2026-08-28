import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface TimelineEvent {
  dateLabel: string;
  title: string;
  amountLabel: string;
  positive: boolean;
}

/** Linha do tempo de eventos financeiros do design V4 (docs/screens/dashboard, docs/screens/projecao). */
@Component({
  selector: 'mf-timeline',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="mf-timeline">
      <div class="mf-event" *ngFor="let event of events">
        <span class="mf-event-date">{{ event.dateLabel }}</span>
        <div><b>{{ event.title }}</b></div>
        <span [class.positive]="event.positive" [class.negative]="!event.positive">{{ event.amountLabel }}</span>
      </div>
    </div>
  `,
  styles: [
    `
      .mf-timeline {
        padding: 0.25rem 0.5rem;
      }
      .mf-event {
        border-left: 1px solid var(--mf-line, #e8e1d8);
        padding: 0 0 1.1rem 1.1rem;
        position: relative;
        font-size: 0.8rem;
      }
      .mf-event::before {
        content: '';
        position: absolute;
        left: -5px;
        top: 3px;
        width: 9px;
        height: 9px;
        border-radius: 50%;
        background: var(--mf-primary, #df5830);
        box-shadow: 0 0 0 4px var(--mf-primary-tint, rgba(223, 88, 48, 0.12));
      }
      .mf-event-date {
        font-size: 0.65rem;
        color: var(--mf-muted, #8b8175);
      }
      .positive {
        color: var(--mf-green, #12966f);
      }
      .negative {
        color: var(--mf-red, #c0392b);
      }
    `,
  ],
})
export class TimelineComponent {
  @Input({ required: true }) events!: TimelineEvent[];
}
