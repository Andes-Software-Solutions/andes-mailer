import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Permission } from '@core/api/permission.model';
import { routePaths } from '@core/navigation/route-paths';
import { PermissionMode, SessionStore } from './session-store';

/**
 * Opens a route only for someone who holds the permissions in the active team or globally;
 * anyone else lands on the home page.
 */
export function permissionGuard(
  required: Permission | readonly Permission[],
  mode: PermissionMode = 'any',
): CanActivateFn {
  return async () => {
    const session = inject(SessionStore);
    const router = inject(Router);

    await session.ensureLoaded();

    return session.can(required, mode) ? true : router.parseUrl(`/${routePaths.home}`);
  };
}
