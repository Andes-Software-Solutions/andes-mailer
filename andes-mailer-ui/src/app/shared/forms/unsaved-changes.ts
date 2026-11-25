import { DOCUMENT } from '@angular/common';
import { DestroyRef, Service, inject } from '@angular/core';
import { HasUnsavedChanges } from './unsaved-changes-guard';

/**
 * The pages on screen that can hold unsaved edits, for changes that replace a page's data without
 * navigating, such as switching team. `unsavedChangesGuard` covers navigation.
 */
@Service()
export class UnsavedChanges {
  private readonly _view = inject(DOCUMENT).defaultView;
  private readonly _pages = new Set<HasUnsavedChanges>();

  /** Tracks `page` until `destroyRef` fires. */
  track(page: HasUnsavedChanges, destroyRef: DestroyRef): void {
    this._pages.add(page);
    destroyRef.onDestroy(() => this._pages.delete(page));
  }

  /** True when nothing is unsaved, or the person agrees to lose it. */
  confirmDiscard(question: string): boolean {
    const unsaved = [...this._pages].some((page) => page.hasUnsavedChanges());

    return !unsaved || (this._view?.confirm(`You have unsaved changes. ${question}`) ?? true);
  }
}
