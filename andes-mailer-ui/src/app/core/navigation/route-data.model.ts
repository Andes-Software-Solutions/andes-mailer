import { Permission } from '@core/api/permission.model';

/** What a shell child route declares in `data` for the shell to read. */
export interface ShellRouteData {
  /** The page name in the top bar. */
  readonly label?: string;
  /** What its `permissionGuard` requires, so a team switch can leave a page it no longer permits. */
  readonly permissions?: readonly Permission[];
}
