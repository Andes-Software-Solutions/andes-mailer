import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { MsalGuard } from '@azure/msal-angular';

/**
 * `MsalGuard` as a functional guard. It sends a signed-out user straight to Entra, so the app has
 * no login screen, and sends a failed sign-in to `loginFailedRoute`.
 */
export const authGuard: CanActivateFn = (route, state) =>
  inject(MsalGuard).canActivate(route, state);
