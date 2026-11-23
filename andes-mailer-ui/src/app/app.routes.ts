import { Route, Routes } from '@angular/router';
import { Permission } from '@core/api/permission.model';
import { accessGuard } from '@core/auth/access-guard';
import { authGuard } from '@core/auth/auth-guard';
import { noAccessGuard } from '@core/auth/no-access-guard';
import { permissionGuard } from '@core/auth/permission-guard';
import { navItems } from '@core/navigation/nav-items';
import { ShellRouteData } from '@core/navigation/route-data.model';
import { routePaths } from '@core/navigation/route-paths';

/** A page that isn't built yet; its feature replaces the route when it lands. */
function placeholder(path: string, label: string, permissions?: readonly Permission[]): Route {
  const data: ShellRouteData = permissions ? { label, permissions } : { label };

  return {
    path,
    title: `${label} — Andes Mailer`,
    data,
    canActivate: permissions ? [permissionGuard(permissions)] : [],
    loadComponent: () => import('@features/coming-soon/coming-soon').then((m) => m.ComingSoon),
  };
}

export const routes: Routes = [
  {
    path: routePaths.loginFailed,
    loadComponent: () => import('@features/auth/login-failed').then((m) => m.LoginFailed),
    title: "We couldn't sign you in — Andes Mailer",
  },
  {
    // The access check is a child guard so it runs after authGuard has signed the person in.
    path: routePaths.noAccess,
    canActivate: [authGuard],
    canActivateChild: [noAccessGuard],
    children: [
      {
        path: '',
        loadComponent: () => import('@features/auth/no-access').then((m) => m.NoAccess),
        title: 'No access — Andes Mailer',
      },
    ],
  },
  {
    // The shell and the landing page are lazy on purpose: neither can render before sign-in and the
    // account load finish, and keeping them (and Bootstrap's JavaScript) out of the initial bundle
    // speeds up the sign-in screens. accessGuard prefetches the shell while the account loads.
    path: '',
    loadComponent: () => import('@core/layout/shell').then((m) => m.Shell),
    canActivate: [authGuard],
    canActivateChild: [accessGuard],
    children: [
      {
        path: routePaths.home,
        pathMatch: 'full',
        loadComponent: () => import('@features/home/home').then((m) => m.Home),
        title: 'Andes Mailer',
        data: { label: 'Home' } satisfies ShellRouteData,
      },
      ...navItems.map((item) => placeholder(item.path, item.label, item.permissions)),
      placeholder(routePaths.profile, 'My profile'),
      placeholder(routePaths.help, 'Help & guides'),
    ],
  },
  { path: '**', redirectTo: '' },
];
