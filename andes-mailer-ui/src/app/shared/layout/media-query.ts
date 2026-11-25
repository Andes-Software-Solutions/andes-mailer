import { DOCUMENT } from '@angular/common';
import { DestroyRef, Signal, inject, signal } from '@angular/core';

/** Bootstrap's md breakpoint: below it, tables become cards. */
export const PHONE_QUERY = '(max-width: 767.98px)';

/** Bootstrap's lg breakpoint: below it, the less important table columns fold into a row. */
export const TABLET_DOWN_QUERY = '(max-width: 991.98px)';

/**
 * Whether a media query matches, kept up to date. Call it in an injection context. `false` where
 * the environment has no `matchMedia`, such as a test DOM.
 */
export function mediaQuery(query: string): Signal<boolean> {
  const list = inject(DOCUMENT).defaultView?.matchMedia?.(query);
  const matches = signal(list?.matches ?? false);

  if (list) {
    const update = (event: MediaQueryListEvent): void => matches.set(event.matches);

    list.addEventListener('change', update);
    inject(DestroyRef).onDestroy(() => list.removeEventListener('change', update));
  }

  return matches.asReadonly();
}
