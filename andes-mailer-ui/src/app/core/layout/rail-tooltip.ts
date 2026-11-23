import { DOCUMENT } from '@angular/common';
import { DestroyRef, Directive, ElementRef, afterNextRender, inject, input } from '@angular/core';
import { Tooltip } from 'bootstrap';

/** The tablet width at which the sidebar collapses to an icon rail. */
export const RAIL_QUERY = '(min-width: 768px) and (max-width: 991.98px)';

/**
 * Names an icon-only rail item on hover and focus. Enabled only while the sidebar is a rail,
 * since the full sidebar already shows the label. An empty label adds no tooltip.
 */
@Directive({ selector: '[appRailTooltip]' })
export class RailTooltip {
  readonly appRailTooltip = input('');

  constructor() {
    const host = inject<ElementRef<HTMLElement>>(ElementRef);
    const document = inject(DOCUMENT);
    const destroyRef = inject(DestroyRef);

    afterNextRender(() => {
      const label = this.appRailTooltip();

      if (!label) {
        return;
      }

      const tooltip = new Tooltip(host.nativeElement, {
        title: label,
        placement: 'right',
        trigger: 'hover focus',
      });
      const media = document.defaultView?.matchMedia?.(RAIL_QUERY);
      const sync = (): void => {
        if (media?.matches) {
          tooltip.enable();
        } else {
          tooltip.hide();
          tooltip.disable();
        }
      };

      sync();
      media?.addEventListener('change', sync);
      destroyRef.onDestroy(() => {
        media?.removeEventListener('change', sync);
        tooltip.dispose();
      });
    });
  }
}
