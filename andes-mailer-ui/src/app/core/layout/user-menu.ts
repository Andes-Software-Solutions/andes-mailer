import {
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  booleanAttribute,
  computed,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionStore } from '@core/auth/session-store';
import { routePaths } from '@core/navigation/route-paths';
import { ThemePreference } from '@core/theme/theme-preference';
import { ThemeStore } from '@core/theme/theme-store';
import { initials } from '@shared/formatting/initials';
import { attachDropdown } from './attach-dropdown';
import { roleBadge } from './role-badge';

interface ThemeOption {
  readonly value: ThemePreference;
  readonly label: string;
  readonly icon: string;
}

const THEME_OPTIONS: readonly ThemeOption[] = [
  { value: 'light', label: 'Light', icon: 'bi-sun' },
  { value: 'dark', label: 'Dark', icon: 'bi-moon-stars' },
  { value: 'system', label: 'Same as system', icon: 'bi-circle-half' },
];

/** The account button: profile and theme. There is no sign-out; the app is meant to stay signed in. */
@Component({
  selector: 'app-user-menu',
  imports: [RouterLink],
  styleUrl: './user-menu.scss',
  templateUrl: './user-menu.html',
})
export class UserMenu {
  private readonly _session = inject(SessionStore);
  private readonly _toggle = viewChild.required<ElementRef<HTMLButtonElement>>('toggle');
  protected readonly theme = inject(ThemeStore);

  /** Avatar only, for the mobile top bar. */
  readonly compact = input(false, { transform: booleanAttribute });

  protected readonly themeOptions = THEME_OPTIONS;
  protected readonly profilePath = `/${routePaths.profile}`;

  protected readonly name = computed(() => this._session.user()?.name ?? '');
  protected readonly email = computed(() => this._session.user()?.email ?? '');
  protected readonly initials = computed(() => initials(this.name()));
  protected readonly subtitle = computed(
    () =>
      roleBadge(this._session.activeTeam()?.role ?? null, this._session.isAdministrator())?.label ??
      this.email(),
  );

  constructor() {
    const destroyRef = inject(DestroyRef);

    afterNextRender(() => attachDropdown(this._toggle().nativeElement, destroyRef));
  }

  protected setTheme(preference: ThemePreference): void {
    this.theme.setPreference(preference);
  }
}
