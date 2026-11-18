# Contact groups

Teams write to the same people again and again: the board, a department, a list of partners. Without a shared list, each member retypes the addresses, and a new distribution list in the directory means an IT ticket. A contact group is a named list of addresses that a team keeps in the app, so a team administrator maintains it and any member adds it to an email in one step. This page covers the data model, the `/api/teams/{teamId}/contact-groups` endpoints, how a group's addresses reach an email, how changes are tracked, and the migration that adds groups. For the drafts a group feeds, see [Emails and attachments](emails-and-attachments.md). For teams, see [Teams](teams.md). For who may do what, see [Users and permissions](users-and-permissions.md). For how tokens are issued and validated, see [Authentication](authentication.md).

## How it fits together

- A **contact group** belongs to a [team](teams.md). It holds a name, an optional description, and 1 to 500 addresses, listed in the order you entered them.
- A **member** is a plain email address. It can belong to someone in the directory or to anyone outside it. For directory accounts, the API also stores the Entra object id and the display name, so a client can show names without a second lookup.
- **Reading** groups is open to every member of the team. **Creating, changing, and deactivating** them needs the new `ManageTeamContactGroups` permission, which a `TeamAdministrator` holds and a `TeamMember` does not. A global `Administrator` holds it in every team.
- Adding a group to an email **copies its addresses** into the email's recipients. The email does not point back to the group, so later changes to the group never reach a draft that already has them. See [Use a group in an email](#use-a-group-in-an-email).
- Every change is recorded: who created and deactivated a group, and who added and removed each address, with the dates. See [Audit trail](#audit-trail).

### Out of scope for now

These are not built, and the API makes no provision for them:

- The UI: there are no contact group screens yet.
- A server-side "add this group to the email" route. The client expands the group; see [Use a group in an email](#use-a-group-in-an-email).
- Groups inside groups, and groups shared across teams.
- Reactivating a deactivated group.
- An endpoint that reads a group's membership history. The rows are kept; nothing exposes them yet.

## Data model

Two tables live in the `Core` schema and arrive in the migration `Andes.Mailer.Repository/Sql/Migrations/*_AddContactGroups.cs`.

### `[Core].[ContactGroup]`

| Column | Notes |
| --- | --- |
| `Id` | A GUID the API generates. |
| `TeamId` | The team the group belongs to, a foreign key to `[Core].[Team]`. Every query filters on it. |
| `Name` | Up to 100 characters, trimmed. Unique among the team's **active** groups only (a filtered unique index on `TeamId` and `Name` where `DateDeactivated IS NULL`), so a deactivated group frees its name. |
| `Description` | Up to 500 characters. Null when you send nothing or only whitespace. |
| `CreatedById` | The Entra object id of the user who created the group, a foreign key to `[Core].[User]`. |
| `DeactivatedById` | The object id of the user who deactivated the group, a foreign key to `[Core].[User]`. Null while the group is active. |
| `DateCreated`, `DateModified`, `DateDeactivated`, `Version` | The same audit, soft-delete, and `rowversion` columns as [`[Core].[User]`](users-and-permissions.md#coreuser). |

Deactivating the team hides its groups with it (the `TeamDeactivation` query filter), without touching their rows.

### `[Core].[ContactGroupMember]`

| Column | Notes |
| --- | --- |
| `ContactGroupId`, `Address` | The address is trimmed and stored in lowercase, up to 256 characters. A filtered unique index on the pair, where `DateDeactivated IS NULL`, means an address appears once **among the group's active members**. |
| `UserId` | The Entra object id when Graph found an account that owns the address; null for an address outside the directory. It is not a foreign key: a member can be anyone in the directory, not only a user of this application. |
| `DisplayName` | The directory account's first and last name joined by a space, cut to 256 characters if longer. Null for an address outside the directory. |
| `Position` | The address's index in the request's `members` array, so the entry order survives a reload. |
| `AddedById` | The object id of the user who added the address, a foreign key to `[Core].[User]`. |
| `RemovedById` | The object id of the user who removed the address, a foreign key to `[Core].[User]`. Null while the address is in the group. |
| `DateCreated`, `DateModified`, `DateDeactivated`, `Version` | The same audit, soft-delete, and `rowversion` columns as above. |

Unlike the recipients of an email or a template, a member is **soft-deleted** when you remove it, so the table keeps the full membership history. A deactivated group hides its members (the `GroupDeactivation` query filter).

The limits are in `ContactGroupLimits` (`Andes.Mailer.Common/Constants/ContactGroups/`). The member cap, the address length, and the display name length are taken from the email limits, so a whole group always fits in one email.

## Permissions

| Action | Permission in the team |
| --- | --- |
| List and read groups | `ComposeTeamEmails` |
| Create, replace, and deactivate a group | `ManageTeamContactGroups` |

`ManageTeamContactGroups` (15) is described as "Creates, changes, and deactivates a team's contact groups". `TeamAdministrator` and the global `Administrator` hold it; `TeamMember` does not. Reading needs only `ComposeTeamEmails`, which both team roles hold, because every member adds groups to emails. The grants are in [Users and permissions](users-and-permissions.md#roles-and-permissions).

All five routes run the team check first (see [How team access is checked](teams.md#how-team-access-is-checked)). The consequences:

- A caller who is not in the team, or a team that is missing or deactivated, gets 404. A group in another team returns the same 404 as one that does not exist.
- A `TeamMember` who calls a write route gets 403, naming the missing permission. The team check ran first, so that member is already in the team and nothing is hidden.
- A global `Administrator` passes every check for every active team.

## Endpoints

All routes need a token with the `access_as_user` scope, like every other endpoint. Errors are Problem Details responses. Property names are camelCase.

| Method and route | Permission | Success | Other statuses |
| --- | --- | --- | --- |
| `GET /api/teams/{teamId}/contact-groups` | `ComposeTeamEmails` | 200 with one page of the team's groups, ordered by name | 400 for a bad `search`, `page`, or `pageSize`; 403; 404 |
| `GET /api/teams/{teamId}/contact-groups/{groupId}` | `ComposeTeamEmails` | 200 with the group and its members | 403; 404 |
| `POST /api/teams/{teamId}/contact-groups` | `ManageTeamContactGroups` | 201 with the group and a `Location` of `/api/teams/{teamId}/contact-groups/{id}` | 400; 403; 404; 409 if an active group of the team has the name |
| `PUT /api/teams/{teamId}/contact-groups/{groupId}` | `ManageTeamContactGroups` | 200 with the updated group | 400; 403; 404; 409 if the `version` is stale or another active group has the name |
| `DELETE /api/teams/{teamId}/contact-groups/{groupId}` | `ManageTeamContactGroups` | 204 | 403; 404; 409 if another request changed the group at the same moment |

### The group object

```json
{
  "id": "0199fc10-7a21-7e4b-9c3d-2f1a0b9c8d70",
  "name": "Board",
  "description": "Quarterly board mailing",
  "members": [
    { "address": "ana.rojas@andes.example", "userId": "4ee09cb0-d7e7-4be4-b82f-2674695eb9df", "displayName": "Ana Rojas" },
    { "address": "auditor@outside.example", "userId": null, "displayName": null }
  ],
  "dateCreated": "2026-11-18T23:20:00+00:00",
  "dateModified": null,
  "version": "AAAAAAAAB9E="
}
```

`members` keeps the order you entered. `userId` and `displayName` are null for an address outside the directory. `version` is the base64 row version; send it back unchanged when you update the group.

### List groups

```http
GET /api/teams/0199fa10-3c4d-7e21-b6a0-9d8c7b6a5f40/contact-groups?search=board&page=1&pageSize=25
```

| Parameter | Default | Range |
| --- | --- | --- |
| `search` | None | Up to 100 characters. Returns the groups whose **name contains** the text, ignoring case. Without it, every active group. |
| `page` | 1 | 1 to 21474836 |
| `pageSize` | 25 | 1 to 100 |

Groups are ordered by name, with ties broken by id. The response is the same wrapper as [`GET /api/users`](users-and-permissions.md#list-users), and each item omits the members:

```json
{
  "items": [
    { "id": "0199fc10-7a21-7e4b-9c3d-2f1a0b9c8d70", "name": "Board", "description": "Quarterly board mailing", "memberCount": 2 }
  ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 1
}
```

A page past the end returns an empty `items` array. Read one group with `GET .../contact-groups/{groupId}` to get its members.

### Create a group

```http
POST /api/teams/0199fa10-3c4d-7e21-b6a0-9d8c7b6a5f40/contact-groups
Content-Type: application/json

{
  "name": "Board",
  "description": "Quarterly board mailing",
  "members": [
    { "address": "ana.rojas@andes.example" },
    { "address": "auditor@outside.example" }
  ]
}
```

The API looks each address up in Microsoft Graph and stores the account's id and display name for the ones it finds, as it does for [email recipients](emails-and-attachments.md#recipient-resolution). A Graph failure surfaces as a 500, with the cause in the logs.

The rules, for create and replace:

| Field | Rule |
| --- | --- |
| `name` | Required, at most 100 characters, unique among the team's active groups. A taken name returns 409. |
| `description` | Optional, at most 500 characters. |
| `members` | Required, 1 to 500 entries, no null entries, no repeated address. Case and surrounding spaces are ignored when comparing. |
| `members[].address` | A single plain address of at most 256 characters: no display name, no spaces. Any such address is allowed, inside or outside the directory. |
| `version` (replace only) | The base64 row version from the last response, decoding to 8 bytes. |

Email recipients and group members share one address rule (`EmailAddressRules`), so any address a group accepts is also accepted as a recipient.

### Replace a group

```http
PUT /api/teams/0199fa10-3c4d-7e21-b6a0-9d8c7b6a5f40/contact-groups/0199fc10-7a21-7e4b-9c3d-2f1a0b9c8d70
Content-Type: application/json

{
  "name": "Board",
  "description": "Quarterly board mailing",
  "members": [
    { "address": "ana.rojas@andes.example" },
    { "address": "new.director@andes.example" }
  ],
  "version": "AAAAAAAAB9E="
}
```

`PUT` **replaces** the name, description, and members: send the full list each time, because an address you leave out is removed. What happens to each address:

| Address in the request | Result |
| --- | --- |
| Already an active member | The row stays, with its new position. There is no new directory lookup, so its name does not refresh. |
| Not yet a member | A new row, looked up in the directory. |
| An active member that the request leaves out | The row is deactivated and stamped with who removed it. |

Adding back an address that was removed creates a **new row**; the old one stays as history. The response holds the new `version`.

### Deactivate a group

`DELETE` soft-deletes the group and stamps `DeactivatedById`. Its members keep their rows, which the group's filter hides with it. Emails that already hold the group's addresses are left alone. The name is free for a new group straight away.

## Use a group in an email

There is no route that adds a group to an email. The client does it, so the compose screen shows the addresses before anyone sends. To add a group:

1. `GET /api/teams/{teamId}/contact-groups/{groupId}`.
2. Add each `members[].address` to the email's `recipients` under the header the user picked (`To`, `Cc`, or `Bcc`).
3. Save the draft with [`POST` or `PUT /api/teams/{teamId}/emails`](emails-and-attachments.md#create-and-update-a-draft).

The addresses become ordinary recipients, so they follow the usual rules: the draft's 500-address cap and no repeated address across the headers. A group holds at most 500 addresses, so it always fits in an empty draft; a draft that already has recipients can still go over, so the client skips the addresses the draft already has. The draft's own save looks the addresses up in the directory again.

Expanding happens at that moment. If the group changes afterwards, drafts that already took its addresses keep the old ones, and deactivating a group does not touch any email.

## Concurrency

`PUT` applies only if the row still has the `version` you sent. If anyone changed the group in between, such as a second tab or another team administrator, the API returns 409 and you read the group again before retrying. Without this, two editors would silently overwrite each other.

Changing only the members still bumps the `version`, even though no column of the group changed, so a stale client cannot overwrite a membership change either.

## Audit trail

The API keeps who did what and when, in the tables themselves:

| What | Where |
| --- | --- |
| Who created a group, and when | `[Core].[ContactGroup].[CreatedById]`, `DateCreated` |
| Who deactivated a group, and when | `DeactivatedById`, `DateDeactivated` |
| Who added an address, and when | `[Core].[ContactGroupMember].[AddedById]`, `DateCreated` |
| Who removed an address, and when | `RemovedById`, `DateDeactivated` |

The ids are Entra object ids and foreign keys to `[Core].[User]`. Users are soft-deleted, so a person who leaves never breaks the history. Because removing an address soft-deletes its row and adding it again creates a new one, the full membership history of a group is kept. Nothing reads it through the API yet; query the tables.

## Upgrading: the `AddContactGroups` migration

The migration `20261118231741_AddContactGroups` creates `[Core].[ContactGroup]` and `[Core].[ContactGroupMember]`, and seeds the `ManageTeamContactGroups` permission (15) with grants to `Administrator` and `TeamAdministrator`. It deletes no data. Apply it from `andes-mailer-api/`:

```bash
dotnet ef database update --project Andes.Mailer.Repository --startup-project Andes.Mailer.Api
```

Existing role holders get the permission through their role; nobody needs a new grant.

## Known limitations

- **No UI.** The UI has no screens for contact groups yet.
- **Member names do not refresh.** A member's `userId` and `displayName` are set when the address is added, and an address that stays in the group across `PUT` requests keeps them. An address that had no account when you added it keeps `userId` null until you remove it and add it again.
- **No reactivation.** A deactivated group cannot come back; create a new one.
- **Expanding is the client's job.** A group is not a live reference: an email keeps the addresses it took.
- **Graph failures return 500.** A Graph outage, a missing consent, or a bad credential surfaces as a 500 from `POST` and `PUT`, not a specific error.
