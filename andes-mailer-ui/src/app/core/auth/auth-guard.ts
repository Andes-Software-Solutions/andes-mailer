import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { routePaths } from '@core/navigation/route-paths';
import { Auth } from './auth';

/**
 * Sends a signed-out user straight to Entra, so the app has no login screen. Only one automatic
 * attempt per trip: without that limit, a return with no account would loop through Entra forever
 * and Back would trap a user who abandons the Microsoft page.
 */
export const authGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(Auth);
  const router = inject(Router);
  const loginFailed = router.parseUrl(`/${routePaths.loginFailed}`);

  const { account, failed } = await auth.ready();

  if (account) {
    return true;
  }

  if (failed || auth.signInAttempted) {
    return loginFailed;
  }

  try {
    await auth.signIn(state.url);
  } catch (cause) {
    console.error(cause);

    return loginFailed;
  }

  return false;
};
