import { Permission } from '@core/api/permission.model';
import { routePaths } from './route-paths';

export interface NavItem {
  readonly label: string;
  /** A Bootstrap Icons name, without the `bi-` prefix. */
  readonly icon: string;
  readonly path: string;
  /** Any one of these shows the item and opens its route; checked in the active team. */
  readonly permissions: readonly Permission[];
  readonly section: 'main' | 'admin';
}

/** The sidebar, and the permissions its routes require. */
export const navItems: readonly NavItem[] = [
  {
    label: 'Compose',
    icon: 'pencil-square',
    path: routePaths.compose,
    permissions: ['ComposeTeamEmails'],
    section: 'main',
  },
  {
    label: 'Drafts',
    icon: 'file-earmark-text',
    path: routePaths.drafts,
    permissions: ['ComposeTeamEmails'],
    section: 'main',
  },
  {
    label: 'Scheduled',
    icon: 'clock',
    path: routePaths.scheduled,
    permissions: ['ComposeTeamEmails'],
    section: 'main',
  },
  {
    label: 'Sent',
    icon: 'send',
    path: routePaths.sent,
    permissions: ['ComposeTeamEmails'],
    section: 'main',
  },
  {
    label: 'Templates',
    icon: 'layout-text-window-reverse',
    path: routePaths.templates,
    permissions: ['ComposeTeamEmails'],
    section: 'main',
  },
  {
    label: 'Email groups',
    icon: 'person-lines-fill',
    path: routePaths.emailGroups,
    permissions: ['ComposeTeamEmails'],
    section: 'main',
  },
  {
    label: 'Team members',
    icon: 'people',
    path: routePaths.teamMembers,
    permissions: ['ModifyTeamUser'],
    section: 'admin',
  },
  {
    label: 'Team settings',
    icon: 'gear',
    path: routePaths.teamSettings,
    permissions: ['ModifyTeam'],
    section: 'admin',
  },
  {
    // CreateTeam comes from the global Administrator role only, never from a team role.
    label: 'Teams',
    icon: 'diagram-3',
    path: routePaths.teams,
    permissions: ['CreateTeam'],
    section: 'admin',
  },
];
