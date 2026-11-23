import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { routePaths } from '@core/navigation/route-paths';
import { SessionStore } from './session-store';

/**
 * For the shell's `canActivateChild`, so it runs only after the shell's `authGuard` has signed the
 * person in: guards in one array run concurrently, and the API call needs a token. A failed load
 * cancels the navigation and keeps its URL for the sign-in splash's retry.
 */
export const accessGuard: CanActivateChildFn = async (_, state) => {
  const session = inject(SessionStore);
  const router = inject(Router);

  // The shell chunk only loads once the guards pass; fetching it alongside the account saves a round trip.
  void import('@core/layout/shell');

  await session.ensureLoaded();

  if (session.status() !== 'loaded') {
    session.holdForRetry(state.url);

    return false;
  }

  return session.hasAccess() ? true : router.parseUrl(`/${routePaths.noAccess}`);
};
