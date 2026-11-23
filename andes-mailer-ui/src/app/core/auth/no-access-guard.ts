import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { routePaths } from '@core/navigation/route-paths';
import { SessionStore } from './session-store';

/**
 * Keeps the no-access page for accounts without access; anyone with access goes home. A
 * `canActivateChild`, for the same reason as `accessGuard`: it must run after `authGuard`.
 */
export const noAccessGuard: CanActivateChildFn = async (_, state) => {
  const session = inject(SessionStore);
  const router = inject(Router);

  await session.ensureLoaded();

  if (session.status() !== 'loaded') {
    session.holdForRetry(state.url);

    return false;
  }

  return session.hasAccess() ? router.parseUrl(`/${routePaths.home}`) : true;
};
