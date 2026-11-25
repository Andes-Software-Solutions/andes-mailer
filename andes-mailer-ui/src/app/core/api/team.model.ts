import { Role } from './permission.model';

/** The preset colours of a team's avatar, by the API's name. */
export type TeamColor = 'Mint' | 'Seafoam' | 'Mist' | 'Sky' | 'Green' | 'Ocean';

export const teamColors: readonly TeamColor[] = [
  'Mint',
  'Seafoam',
  'Mist',
  'Sky',
  'Green',
  'Ocean',
];

/** What a colour swatch is called to assistive technology. */
export const teamColorLabels: Readonly<Record<TeamColor, string>> = {
  Mint: 'Light mint',
  Seafoam: 'Seafoam',
  Mist: 'Mist',
  Sky: 'Sky',
  Green: 'Andes green',
  Ocean: 'Ocean blue',
};

/** A team and its settings, as `GET /api/teams/{id}` returns it. */
export interface Team {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
  readonly serviceAccountEmail: string;
  readonly avatarColor: TeamColor;
  /** Sanitized HTML. */
  readonly defaultSignature: string | null;
  readonly confirmRecipientsAbove: number;
  readonly memberCount: number;
  readonly administratorCount: number;
  readonly dateCreated: string;
  readonly dateModified: string | null;
  /** The row version; send it back unchanged to update the team. */
  readonly version: string;
}

/** A team in `GET /api/teams`. */
export interface TeamSummary {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
  readonly serviceAccountEmail: string;
  readonly avatarColor: TeamColor;
  readonly memberCount: number;
  /** The caller's role, or `null` when they see the team through a global role only. */
  readonly role: Role | null;
}

export interface CreateTeamRequest {
  readonly name: string;
  readonly description: string | null;
  readonly serviceAccountEmail: string;
  readonly avatarColor: TeamColor;
}

/** `PUT` replaces every field, so send them all. */
export interface UpdateTeamRequest {
  readonly name: string;
  readonly description: string | null;
  readonly serviceAccountEmail: string;
  readonly avatarColor: TeamColor;
  readonly defaultSignature: string | null;
  readonly confirmRecipientsAbove: number;
  readonly version: string;
}
