import { Role } from '@core/api/permission.model';

export interface RoleBadge {
  readonly label: string;
  readonly cssClass: 'badge-admin' | 'badge-member';
}

/**
 * What the team switcher shows beside a team. A global administrator administers every team,
 * whatever their membership says.
 */
export function roleBadge(teamRole: Role | null, isGlobalAdministrator: boolean): RoleBadge | null {
  if (isGlobalAdministrator || teamRole === 'TeamAdministrator') {
    return { label: 'Administrator', cssClass: 'badge-admin' };
  }

  return teamRole === 'TeamMember' ? { label: 'Member', cssClass: 'badge-member' } : null;
}
