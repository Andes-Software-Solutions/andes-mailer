import {
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  ActivatedRouteSnapshot,
  NavigationEnd,
  Router,
  RouterLink,
  RouterOutlet,
} from '@angular/router';
import { HasPermission } from '@core/auth/has-permission';
import { ToastStack } from '@core/feedback/toast-stack';
import { SessionStore } from '@core/auth/session-store';
import { ShellRouteData } from '@core/navigation/route-data.model';
import { routePaths } from '@core/navigation/route-paths';
import { AppLogo } from '@shared/brand/app-logo';
import { Offcanvas } from 'bootstrap';
import { filter, map } from 'rxjs';
import { SidebarContent } from './sidebar-content';
import { TeamSwitcher } from './team-switcher';
import { ThemeToggle } from './theme-toggle';
import { UserMenu } from './user-menu';

/** Bootstrap's md breakpoint: from here the sidebar or rail replaces the phone menu. */
const SIDEBAR_QUERY = '(min-width: 768px)';

/**
 * The signed-in frame: sidebar (an icon rail at tablet width, a slide-in menu on phones), top bar
 * with the theme toggle and account menu, and the page. Lazy, so Bootstrap's JavaScript stays out
 * of the bundle the sign-in screens need.
 */
@Component({
  selector: 'app-shell',
  imports: [
    AppLogo,
    HasPermission,
    RouterLink,
    RouterOutlet,
    SidebarContent,
    TeamSwitcher,
    ThemeToggle,
    ToastStack,
    UserMenu,
  ],
  styleUrl: './shell.scss',
  templateUrl: './shell.html',
})
export class Shell {
  private readonly _router = inject(Router);
  private readonly _session = inject(SessionStore);
  private readonly _menu = viewChild.required<ElementRef<HTMLElement>>('menu');
  private readonly _menuButton = viewChild.required<ElementRef<HTMLButtonElement>>('menuButton');
  private readonly _main = viewChild.required<ElementRef<HTMLElement>>('main');
  private _offcanvas: Offcanvas | null = null;

  protected readonly paths = routePaths;

  private readonly _page = toSignal(
    this._router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => this.deepestData()),
    ),
    { initialValue: this.deepestData() },
  );

  protected readonly pageLabel = computed(() => this._page().label ?? '');

  constructor() {
    const destroyRef = inject(DestroyRef);
    const injector = inject(Injector);

    afterNextRender(() => {
      const menu = this._menu().nativeElement;
      const wide = menu.ownerDocument.defaultView?.matchMedia?.(SIDEBAR_QUERY);
      // Bootstrap returns focus to the opener only for its data-API toggle, not for show(). Only
      // when focus is still in the menu or lost: a followed link has already moved it to the page.
      // Past the phone width the menu button is hidden, so the page takes focus instead.
      const refocus = (): void => {
        const active = menu.ownerDocument.activeElement;

        if (!active || active === menu.ownerDocument.body || menu.contains(active)) {
          (wide?.matches ? this._main() : this._menuButton()).nativeElement.focus();
        }
      };
      // d-md-none hides the open panel on a resize but leaves its backdrop, scroll lock and focus
      // trap, and Bootstrap's own resize handling skips a fixed panel. Checked again once a show
      // finishes, for a resize made mid-transition.
      const closeIfWide = (): void => {
        if (wide?.matches) {
          this._offcanvas?.hide();
        }
      };

      this._offcanvas = Offcanvas.getOrCreateInstance(menu);
      menu.addEventListener('hidden.bs.offcanvas', refocus);
      menu.addEventListener('shown.bs.offcanvas', closeIfWide);
      wide?.addEventListener('change', closeIfWide);
      destroyRef.onDestroy(() => {
        menu.removeEventListener('hidden.bs.offcanvas', refocus);
        menu.removeEventListener('shown.bs.offcanvas', closeIfWide);
        wide?.removeEventListener('change', closeIfWide);
        this._offcanvas?.dispose();
      });
    });

    // A new page takes focus, so keyboard and screen-reader users start at its content. The first
    // navigation is the one that created the shell, where focus belongs to the browser.
    let first = true;
    this._router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => {
        if (first) {
          first = false;

          return;
        }

        afterNextRender(() => this._main().nativeElement.focus(), { injector });
      });

    // Switching to a team that doesn't permit the open page leaves it, as the guard would have.
    effect(() => {
      const required = this._page().permissions;

      if (required && !this._session.can(required)) {
        void this._router.navigateByUrl(`/${routePaths.home}`);
      }
    });
  }

  protected openMenu(): void {
    this._offcanvas?.show();
  }

  protected closeMenu(): void {
    this._offcanvas?.hide();
  }

  private deepestData(): ShellRouteData {
    let route: ActivatedRouteSnapshot = this._router.routerState.snapshot.root;

    while (route.firstChild) {
      route = route.firstChild;
    }

    return route.data as ShellRouteData;
  }
}
