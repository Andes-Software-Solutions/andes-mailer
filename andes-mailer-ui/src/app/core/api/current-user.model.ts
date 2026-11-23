import { Permission, Role } from './permission.model';

/** A team the signed-in user can see, and what they may do in it. */
export interface CurrentUserTeam {
  readonly id: string;
  readonly name: string;
  /** Their role in the team, or `null` when a global role alone lets them see it. */
  readonly role: Role | null;
  /** Their team role's grants together with their global ones. */
  readonly permissions: readonly Permission[];
}

/** The signed-in user, as `GET /api/users/me` returns it. */
export interface CurrentUser {
  /** The Entra object id. */
  readonly id: string;
  readonly name: string;
  readonly email: string | null;
  /** `false` for an account with no active user row; every list is then empty. */
  readonly isActive: boolean;
  /** Global roles only. */
  readonly roles: readonly Role[];
  /** What the global roles grant. */
  readonly permissions: readonly Permission[];
  readonly teams: readonly CurrentUserTeam[];
}
