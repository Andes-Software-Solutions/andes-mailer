import { Component, computed, inject, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { HasPermission } from '@core/auth/has-permission';
import { SessionStore } from '@core/auth/session-store';
import { navItems } from '@core/navigation/nav-items';
import { routePaths } from '@core/navigation/route-paths';
import { AppLogo } from '@shared/brand/app-logo';
import { Ridgeline } from '@shared/brand/ridgeline';
import { RailTooltip } from './rail-tooltip';
import { TeamSwitcher } from './team-switcher';

/**
 * Logo, team switcher, "New email" and the nav: the desktop sidebar (an icon rail at tablet
 * width) and the mobile slide-in menu. Items the active team doesn't permit are left out.
 */
@Component({
  selector: 'app-sidebar-content',
  imports: [
    AppLogo,
    HasPermission,
    RailTooltip,
    Ridgeline,
    RouterLink,
    RouterLinkActive,
    TeamSwitcher,
  ],
  styleUrl: './sidebar-content.scss',
  templateUrl: './sidebar-content.html',
  host: { '[class.rail]': "variant() === 'sidebar'" },
})
export class SidebarContent {
  private readonly _session = inject(SessionStore);

  /** `sidebar` collapses to a rail at tablet width; `offcanvas` is the mobile menu. */
  readonly variant = input<'sidebar' | 'offcanvas'>('sidebar');

  /** Raised when a link is followed or the menu's close button is pressed. */
  readonly dismissed = output();

  protected readonly paths = routePaths;

  protected readonly mainItems = computed(() =>
    navItems.filter((item) => item.section === 'main' && this._session.can(item.permissions)),
  );

  protected readonly adminItems = computed(() =>
    navItems.filter((item) => item.section === 'admin' && this._session.can(item.permissions)),
  );

  protected readonly isRail = computed(() => this.variant() === 'sidebar');

  protected dismiss(): void {
    this.dismissed.emit();
  }
}
