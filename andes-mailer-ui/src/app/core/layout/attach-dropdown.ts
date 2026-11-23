import { DestroyRef } from '@angular/core';
import { Dropdown } from 'bootstrap';

/**
 * Creates the Bootstrap dropdown for a toggle and disposes it with the component. Bootstrap
 * refocuses the toggle only on Escape; choosing an item hides the focused item instead, so focus
 * returns to the toggle whenever it was inside the menu or already lost.
 */
export function attachDropdown(toggle: HTMLElement, destroyRef: DestroyRef): Dropdown {
  const dropdown = Dropdown.getOrCreateInstance(toggle);
  const container = toggle.parentElement;
  const restoreFocus = (): void => {
    const active = toggle.ownerDocument.activeElement;

    if (!active || active === toggle.ownerDocument.body || container?.contains(active)) {
      toggle.focus();
    }
  };

  toggle.addEventListener('hidden.bs.dropdown', restoreFocus);
  destroyRef.onDestroy(() => {
    toggle.removeEventListener('hidden.bs.dropdown', restoreFocus);
    dropdown.dispose();
  });

  return dropdown;
}
