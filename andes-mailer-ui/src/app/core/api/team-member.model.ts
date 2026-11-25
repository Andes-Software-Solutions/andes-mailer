import { Role } from './permission.model';

/** The roles a person can hold in a team. */
export type TeamRole = Extract<Role, 'TeamAdministrator' | 'TeamMember'>;

/** A member of a team, as `GET /api/teams/{teamId}/members` returns it. */
export interface TeamMember {
  /** The Entra object id. */
  readonly userId: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly fullName: string;
  readonly email: string;
  readonly role: TeamRole;
  /** When they joined; a returning member keeps their first join date. */
  readonly dateCreated: string;
  readonly dateModified: string | null;
  /** The membership's row version; send it back to change the member. */
  readonly version: string;
}

export interface AddTeamMemberRequest {
  readonly email: string;
  readonly role: TeamRole;
}

export interface UpdateTeamMemberRequest {
  readonly role: TeamRole;
  readonly version: string;
}
