import { Type } from '@angular/core';
import { Route, Routes } from '@angular/router';
import { Permission } from '@core/api/permission.model';
import { accessGuard } from '@core/auth/access-guard';
import { authGuard } from '@core/auth/auth-guard';
import { noAccessGuard } from '@core/auth/no-access-guard';
import { permissionGuard } from '@core/auth/permission-guard';
import { navItems } from '@core/navigation/nav-items';
import { ShellRouteData } from '@core/navigation/route-data.model';
import { routePaths } from '@core/navigation/route-paths';
import { unsavedChangesGuard } from '@shared/forms/unsaved-changes-guard';

type PageLoader = () => Promise<Type<unknown>>;

/** The built pages; every other nav item opens the coming-soon placeholder until its feature lands. */
const pages: Readonly<Partial<Record<string, PageLoader>>> = {
  [routePaths.teamMembers]: () =>
    import('@features/team-members/team-members').then((m) => m.TeamMembers),
  [routePaths.teamSettings]: () =>
    import('@features/team-settings/team-settings').then((m) => m.TeamSettings),
  [routePaths.teams]: () => import('@features/teams/teams').then((m) => m.Teams),
};

function page(path: string, label: string, permissions?: readonly Permission[]): Route {
  const data: ShellRouteData = permissions ? { label, permissions } : { label };

  return {
    path,
    title: `${label} — Andes Mailer`,
    data,
    canActivate: permissions ? [permissionGuard(permissions)] : [],
    canDeactivate: path === routePaths.teamSettings ? [unsavedChangesGuard] : [],
    loadComponent:
      pages[path] ?? (() => import('@features/coming-soon/coming-soon').then((m) => m.ComingSoon)),
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
      ...navItems.map((item) => page(item.path, item.label, item.permissions)),
      page(routePaths.profile, 'My profile'),
      page(routePaths.help, 'Help & guides'),
    ],
  },
  { path: '**', redirectTo: '' },
];
