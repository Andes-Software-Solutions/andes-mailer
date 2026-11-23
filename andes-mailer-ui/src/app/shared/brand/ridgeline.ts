import { Component } from '@angular/core';

/** The brand's single-line ridge, used under the sidebar logo and on empty states. Decorative. */
@Component({
  selector: 'app-ridgeline',
  host: { 'aria-hidden': 'true' },
  styles: `
    :host {
      display: block;
      color: var(--am-teal);
      line-height: 0;
    }

    svg {
      width: 100%;
      height: 14px;
    }
  `,
  template: `
    <svg viewBox="0 0 228 14" preserveAspectRatio="none" focusable="false">
      <polyline
        points="0,12.5 96,12.5 106,6 113,9 125,1.5 137,10 146,6.5 156,12.5 228,12.5"
        fill="none"
        stroke="currentColor"
        stroke-width="1.25"
        stroke-linejoin="round"
        vector-effect="non-scaling-stroke"
      />
    </svg>
  `,
})
export class Ridgeline {}
