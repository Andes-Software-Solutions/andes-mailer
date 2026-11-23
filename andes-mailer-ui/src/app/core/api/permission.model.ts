/** The API's `Permissions` enum, sent by name. */
export type Permission =
  | 'CreateUser'
  | 'ModifyUser'
  | 'DeactivateUser'
  | 'CreateTeamUser'
  | 'ModifyTeamUser'
  | 'DeactivateTeamUser'
  | 'CanAttachDocuments'
  | 'CreateTeam'
  | 'ModifyTeam'
  | 'DeactivateTeam'
  | 'ViewTeam'
  | 'ComposeTeamEmails'
  | 'ManageTeamTemplates'
  | 'DuplicateTeamEmails'
  | 'ManageTeamContactGroups'
  | 'SendTeamEmails';

/** The API's `Roles` enum, sent by name. `Administrator` is global; the others are held per team. */
export type Role = 'Administrator' | 'TeamAdministrator' | 'TeamMember';
