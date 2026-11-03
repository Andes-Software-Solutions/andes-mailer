import { Routes } from '@angular/router';
import { authGuard } from '@core/auth/auth-guard';
import { routePaths } from '@core/navigation/route-paths';
import { Home } from '@features/home/home';

export const routes: Routes = [
  {
    path: routePaths.home,
    component: Home,
    title: 'Andes Mailer',
    pathMatch: 'full',
    canActivate: [authGuard],
  },
  {
    path: routePaths.loginFailed,
    loadComponent: () => import('@features/auth/login-failed').then((m) => m.LoginFailed),
    title: "We couldn't sign you in — Andes Mailer",
  },
];
