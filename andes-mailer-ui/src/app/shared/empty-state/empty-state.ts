import { Component, input } from '@angular/core';
import { Ridgeline } from '@shared/brand/ridgeline';

/** An icon in an accent disc, a title, a line of text, and an optional projected action. */
@Component({
  selector: 'app-empty-state',
  imports: [Ridgeline],
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.75rem;
      padding: 3rem 1.5rem;
      text-align: center;
    }

    .disc {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 5.75rem;
      height: 5.75rem;
      border-radius: 50%;
      background-color: var(--bs-primary-bg-subtle);
      color: var(--am-accent);
      font-size: 2.5rem;
    }

    .ridge {
      width: 11rem;
      margin-top: -0.5rem;
    }

    .text {
      max-width: 28rem;
      color: var(--bs-secondary-color);
    }
  `,
  template: `
    <div class="disc"><i class="bi" [class]="'bi-' + icon()" aria-hidden="true"></i></div>
    <app-ridgeline class="ridge" />
    <h2 class="h-card mb-0">{{ title() }}</h2>
    @if (text()) {
      <p class="text mb-0">{{ text() }}</p>
    }
    <ng-content />
  `,
})
export class EmptyState {
  /** A Bootstrap Icons name, without the `bi-` prefix. */
  readonly icon = input.required<string>();
  readonly title = input.required<string>();
  readonly text = input('');
}
