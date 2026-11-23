import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { EnvironmentProviders, Provider } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { IPublicClientApplication } from '@azure/msal-browser';
import { CurrentUser, CurrentUserTeam } from '@core/api/current-user.model';
import { Permission } from '@core/api/permission.model';
import { SessionStore } from '@core/auth/session-store';
import { patchState } from '@ngrx/signals';
import { unprotected } from '@ngrx/signals/testing';
import { provideTestAppConfig } from './app-config';
import { provideFakeMsal, signedInMsal } from './msal';

export const meUrl = '/api/users/me';

/** Mirrors the API's grants (`RoleGrantConfiguration`). */
export const teamMemberPermissions: readonly Permission[] = [
  'CanAttachDocuments',
  'ViewTeam',
  'ComposeTeamEmails',
  'DuplicateTeamEmails',
];

export const teamAdministratorPermissions: readonly Permission[] = [
  'CreateTeamUser',
  'ModifyTeamUser',
  'DeactivateTeamUser',
  'CanAttachDocuments',
  'ViewTeam',
  'ComposeTeamEmails',
  'ManageTeamTemplates',
  'DuplicateTeamEmails',
  'ManageTeamContactGroups',
  'SendTeamEmails',
];

export const allPermissions: readonly Permission[] = [
  'CreateUser',
  'ModifyUser',
  'DeactivateUser',
  'CreateTeamUser',
  'ModifyTeamUser',
  'DeactivateTeamUser',
  'CanAttachDocuments',
  'CreateTeam',
  'ModifyTeam',
  'DeactivateTeam',
  'ViewTeam',
  'ComposeTeamEmails',
  'ManageTeamTemplates',
  'DuplicateTeamEmails',
  'ManageTeamContactGroups',
  'SendTeamEmails',
];

export const hrTeam: CurrentUserTeam = {
  id: '6f9619ff-8b86-4011-b42d-00c04fc964ff',
  name: 'HR Announcements',
  role: 'TeamMember',
  permissions: teamMemberPermissions,
};

export const communicationsTeam: CurrentUserTeam = {
  id: '0b1c2d3e-4f50-4617-8293-a4b5c6d7e8f9',
  name: 'Corporate Communications',
  role: 'TeamAdministrator',
  permissions: teamAdministratorPermissions,
};

/** A member of HR Announcements (the first team, so active by default) who administers Communications. */
export const memberUser: CurrentUser = {
  id: 'f1e2d3c4-b5a6-4978-8a9b-0c1d2e3f4a5b',
  name: 'Maria Lopez',
  email: 'maria.lopez@example.com',
  isActive: true,
  roles: [],
  permissions: [],
  teams: [hrTeam, communicationsTeam],
};

export const adminUser: CurrentUser = {
  id: 'a1b2c3d4-e5f6-4789-8abc-def012345678',
  name: 'Ada Lovelace',
  email: 'ada.lovelace@example.com',
  isActive: true,
  roles: ['Administrator'],
  permissions: allPermissions,
  teams: [
    { ...communicationsTeam, role: null, permissions: allPermissions },
    { ...hrTeam, role: null, permissions: allPermissions },
  ],
};

export const noTeamUser: CurrentUser = { ...memberUser, teams: [] };

export const noAccessUser: CurrentUser = {
  id: '11111111-2222-4333-8444-555555555555',
  name: 'Grace Hopper',
  email: 'grace.hopper@example.com',
  isActive: false,
  roles: [],
  permissions: [],
  teams: [],
};

/**
 * Puts a loaded user in the root `SessionStore`, so guards and components skip the request.
 * Needs the MSAL and app-config test providers.
 */
export function seedSession(user: CurrentUser, activeTeamId: string | null = null): void {
  patchState(unprotected(TestBed.inject(SessionStore)), {
    user,
    status: 'loaded',
    activeTeamId,
  });
}

/** What `SessionStore` needs: app config, MSAL around a fake instance, and a testing HTTP backend. */
export function provideSessionTesting(
  instance: IPublicClientApplication = signedInMsal(),
): (Provider | EnvironmentProviders)[] {
  return [
    provideTestAppConfig(),
    ...provideFakeMsal(instance),
    provideHttpClient(),
    provideHttpClientTesting(),
  ];
}
