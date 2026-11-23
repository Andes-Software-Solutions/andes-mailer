import { NgOptimizedImage } from '@angular/common';
import { Component, booleanAttribute, computed, input, numberAttribute } from '@angular/core';

/** The mark's intrinsic size, so any height keeps its proportions. */
const MARK_WIDTH = 663;
const MARK_HEIGHT = 264;

/**
 * The official Andes mark, full colour in the light theme and white in the dark one, with an
 * optional wordmark. Decorative: the surrounding link or heading names it.
 */
@Component({
  selector: 'app-logo',
  imports: [NgOptimizedImage],
  host: { class: 'app-logo' },
  styleUrl: './app-logo.scss',
  template: `
    <img
      class="mark mark-light"
      ngSrc="andes-mark.png"
      alt=""
      [width]="width()"
      [height]="height()"
      [priority]="priority()"
    />
    <img
      class="mark mark-dark"
      ngSrc="andes-mark-white.png"
      alt=""
      [width]="width()"
      [height]="height()"
      [priority]="priority()"
    />
    @if (wordmark()) {
      <span class="word">
        <span class="name"
          >Andes
          @if (product()) {
            <b>{{ product() }}</b>
          }
        </span>
        <span class="sub">Software Solutions</span>
      </span>
    }
  `,
})
export class AppLogo {
  /** The mark's height in pixels. */
  readonly height = input(32, { transform: numberAttribute });
  readonly wordmark = input(true, { transform: booleanAttribute });
  /** The product after "Andes", in the accent colour; empty for the company wordmark. */
  readonly product = input('Mailer');
  /** Set where the logo is the page's largest image, as on the sign-in card. */
  readonly priority = input(false, { transform: booleanAttribute });

  protected readonly width = computed(() => Math.round((this.height() * MARK_WIDTH) / MARK_HEIGHT));
}
