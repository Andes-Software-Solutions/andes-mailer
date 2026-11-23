import { Component, computed, inject } from '@angular/core';
import { ThemeStore } from '@core/theme/theme-store';

/** Flips between light and dark; the user menu also offers following the system. */
@Component({
  selector: 'app-theme-toggle',
  template: `
    <button
      class="btn btn-ghost btn-icon"
      type="button"
      [attr.aria-label]="label()"
      (click)="toggle()"
    >
      <i class="bi" [class]="icon()" aria-hidden="true"></i>
    </button>
  `,
})
export class ThemeToggle {
  private readonly _theme = inject(ThemeStore);

  private readonly _dark = computed(() => this._theme.resolved() === 'dark');

  protected readonly label = computed(() =>
    this._dark() ? 'Switch to light mode' : 'Switch to dark mode',
  );

  protected readonly icon = computed(() => (this._dark() ? 'bi-sun' : 'bi-moon-stars'));

  protected toggle(): void {
    this._theme.toggle();
  }
}
