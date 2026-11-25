import { DOCUMENT } from '@angular/common';
import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';

/** A page that can hold edits the person has not saved yet. */
export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

function holdsChanges(component: unknown): component is HasUnsavedChanges {
  return (
    typeof component === 'object' &&
    component !== null &&
    typeof (component as Partial<HasUnsavedChanges>).hasUnsavedChanges === 'function'
  );
}

/** Asks before leaving a page that reports unsaved changes; pages without the method pass. */
export const unsavedChangesGuard: CanDeactivateFn<unknown> = (component) => {
  if (!holdsChanges(component) || !component.hasUnsavedChanges()) {
    return true;
  }

  const view = inject(DOCUMENT).defaultView;

  return view?.confirm('You have unsaved changes. Leave this page and discard them?') ?? true;
};
