# Teams

Mail has to leave from somewhere. A team is the group of people who write emails together, plus the shared mailbox those emails are sent from, so the message comes from "Press Office" and not from whoever clicked the button. This page covers the data model, the `/api/teams` and `/api/teams/{teamId}/members` endpoints, how team access is checked, the team and member screens in the UI, and what to do before you apply the migration that introduces teams. For roles and permissions, see [Users and permissions](users-and-permissions.md). For the drafts a team composes, see [Emails and attachments](emails-and-attachments.md). For the address lists a team keeps, see [Contact groups](contact-groups.md). For how tokens are issued and validated, see [Authentication](authentication.md). For how every write on this page can be safely repeated, see [Idempotency](idempotency.md).

## How it fits together

- A **team** has a name, an optional description, and a **service account**: the directory mailbox it sends from. The API verifies the mailbox in Microsoft Graph when you save it, so a typo never becomes a team that cannot send.
- A **member** is a user with a role in one team. A user can belong to many teams and hold a different role in each.
- Roles are **scoped**. `Administrator` is held across the application. `TeamAdministrator` and `TeamMember` are held per team. See [Roles and scopes](#roles-and-scopes).
- Only an `Administrator` creates or deactivates teams, and only an `Administrator` changes a team's service mailbox. A `TeamAdministrator` manages who is in their own team and edits its details and [settings](#team-settings).
- A team also keeps **settings** that shape how its emails are written and sent: an avatar colour, where replies go, a default signature, and a confirmation threshold. See [Team settings](#team-settings).
- Every email belongs to a team. Every member of that team can work on all of its drafts.
- A team keeps its own [contact groups](contact-groups.md): named lists of addresses that any member adds to an email, and that a `TeamAdministrator` maintains. Deactivating the team hides its groups, as it hides its emails and templates.

### Out of scope for now

These are not built, and the API makes no provision for them:

- Enforcing the settings. The API stores them and returns them, but no code reads them yet: sending does not ask for a confirmation above `confirmRecipientsAbove`.
- Reactivating a deactivated team. See [Known limitations](#known-limitations).

## Roles and scopes

| Role | Id | Scope | Held through | Can |
| --- | --- | --- | --- | --- |
| `Administrator` | 1 | The whole application | `[Core].[UserRole]` | Everything, in every team, including its emails |
| `TeamAdministrator` | 2 | One team | `[Core].[TeamMember]` | Add, re-role, and remove members; edit the team's details and settings, but not its service mailbox; create, edit, and deactivate [email templates](email-templates.md) and [contact groups](contact-groups.md); compose emails; attach documents |
| `TeamMember` | 3 | One team | `[Core].[TeamMember]` | View the team and its members; compose emails; read [contact groups](contact-groups.md); attach documents |

The scopes are listed in `RoleScopes` (`Andes.Mailer.Common/Constants/Users/`). Check constraints enforce them in the database: `[Core].[UserRole]` accepts only `RoleId` 1, and `[Core].[TeamMember]` accepts only 2 and 3. The API also rejects a global role in a membership request with a 400.

The permission grants for each role are in [Users and permissions](users-and-permissions.md#roles-and-permissions).

## Data model

Both tables live in the `Core` schema and arrive in the migration `Andes.Mailer.Repository/Sql/Migrations/*_AddTeams.cs`. The migration `*_AddTeamServiceAccountIdIndex.cs` adds the unique index on `ServiceAccountId`, and `*_AddTeamSettings.cs` adds the settings columns and the `[Core.Ref].[TeamColor]` reference table described under [Team settings](#team-settings).

### `[Core].[Team]`

| Column | Notes |
| --- | --- |
| `Id` | A GUID the API generates. |
| `Name` | Up to 100 characters, trimmed. Unique among active teams only (a filtered unique index on rows where `DateDeactivated IS NULL`), so a deactivated team does not block its name. |
| `Description` | Up to 500 characters. Null when you send nothing or only whitespace. |
| `ServiceAccountEmail` | The mailbox the team sends from, up to 256 characters, always stored in lowercase. Unique among active teams. |
| `ServiceAccountId` | The Entra object id of that mailbox. Unique among active teams (the filtered unique index `IX_Team_ServiceAccountId`). Not a foreign key: a service account sends mail but never signs in, so it is not a row in `[Core].[User]`. |
| `AvatarColorId` | The colour of the team's avatar, a foreign key to `[Core.Ref].[TeamColor]`. Defaults to `Mint`. |
| `DefaultSignature` | The team's HTML signature. See [Team settings](#team-settings). |
| `ConfirmRecipientsAbove` | The recipient count above which sending should ask for confirmation. See [Team settings](#team-settings). |
| `DateCreated`, `DateModified`, `DateDeactivated`, `Version` | The same audit, soft-delete, and `rowversion` columns as [`[Core].[User]`](users-and-permissions.md#coreuser). |

One mailbox serves one team. The API checks both the address and the object id, because a mailbox's `mail` and `userPrincipalName` are two spellings of the same account. The database enforces both as unique indexes among active teams, so if two concurrent requests use two addresses of one mailbox, one succeeds and the other gets a 409.

### `[Core].[TeamMember]`

| Column | Notes |
| --- | --- |
| `TeamId`, `UserId` | A foreign key to each table. A unique index on the pair covers **every** row, active or not, so a user has at most one membership row per team. |
| `RoleId` | `TeamAdministrator` (2) or `TeamMember` (3), enforced by the `CK_TeamMember_RoleId` check constraint. |
| `DateCreated`, `DateModified`, `DateDeactivated`, `Version` | Removing a member soft-deletes the row. |

Re-adding a removed member reactivates the same row and sets the new role. `dateCreated` therefore keeps the **original** join date.

Queries on `TeamMember` hide a row when the row, its user, or its team is deactivated. Queries on `Email` hide a team's emails when the team is deactivated. A deactivated team or user therefore grants nothing and shows up nowhere, without any row being deleted.

## Team settings

The settings live on `[Core].[Team]` next to the details. A `PUT` replaces all of them, so a client sends every field each time. They are **stored only**: the API validates and returns them, and the compose and send features will read them later.

| Setting | Column | Values and rules |
| --- | --- | --- |
| Avatar colour | `AvatarColorId` | `Mint` (default), `Seafoam`, `Mist`, `Sky`, `Green`, or `Ocean`. The `TeamColors` enum, mirrored by `[Core.Ref].[TeamColor]`. |
| Default signature | `DefaultSignature` | HTML, at most 4000 characters in the request. The API sanitizes it with `EmailBodySanitizer`, the same sanitizer as an email body, and stores null when nothing is left. The column is unbounded, because sanitizing can make the HTML longer. |
| Confirm above | `ConfirmRecipientsAbove` | 1 to 500, the recipient cap of an email. Defaults to 50. Enforced by `CK_Team_ConfirmRecipientsAbove`. |

The migration `*_AddTeamSettings.cs` gives every existing team the defaults a new team gets, so nothing has to be filled in by hand.

A team sends only from its service mailbox, so replies go to that mailbox (the From address, with no Reply-To header), and who reads them is managed in Exchange, through a shared mailbox's permissions or forwarding to a distribution list or Microsoft 365 group.

## Endpoints

All routes need a token with the `access_as_user` scope, like every other endpoint. Errors are Problem Details responses. Enums travel by **name** (`"TeamAdministrator"`), and property names are camelCase.

### Teams

| Method and route | Permission | Success | Other statuses |
| --- | --- | --- | --- |
| `GET /api/teams` | None | 200 with one page of teams | 400 for a bad `page` or `pageSize` |
| `GET /api/teams/{teamId}` | `ViewTeam` in the team | 200 with the team | 403, 404 |
| `POST /api/teams` | `CreateTeam` | 201 with the team and a `Location` of `/api/teams/{id}` | 400, 403, 404, 409 |
| `PUT /api/teams/{teamId}` | `ModifyTeam` in the team | 200 with the updated team | 400, 403, 404, 409 |
| `DELETE /api/teams/{teamId}` | `DeactivateTeam` | 204 | 403, 404, 409 |

`CreateTeam` and `DeactivateTeam` are held by `Administrator` only, so every other caller gets a 403 on those routes. `ModifyTeam` is held by `Administrator` and `TeamAdministrator` and is checked **in the team**: a team administrator edits their own team, and a team member gets a 403. Changing the service mailbox is the exception, described under [Update a team](#update-a-team).

The three writes accept an optional `Idempotency-Key`; see [Idempotency](idempotency.md).

### The team object

```json
{
  "id": "0199fa10-3c4d-7e21-b6a0-9d8c7b6a5f40",
  "name": "Press Office",
  "description": "Media enquiries and announcements",
  "serviceAccountEmail": "press@andes.example",
  "avatarColor": "Mint",
  "defaultSignature": "<p>Press Office</p>",
  "confirmRecipientsAbove": 50,
  "memberCount": 4,
  "administratorCount": 1,
  "dateCreated": "2026-11-15T19:40:02+00:00",
  "dateModified": null,
  "version": "AAAAAAAAB9E="
}
```

`memberCount` and `administratorCount` count active members, so a screen can warn before the last administrator leaves. `version` is the base64 row version. Send it back unchanged when you update the team.

### List teams

```http
GET /api/teams?page=1&pageSize=25
```

`page` and `pageSize` follow the same rules as [`GET /api/users`](users-and-permissions.md#list-users): `pageSize` defaults to 25 and is at most 100. Teams are ordered by name.

What the list contains depends on the caller:

- A global `Administrator` sees **every** active team.
- Anyone else sees only the teams they belong to.

Each item is a summary that carries the caller's own role:

```json
{
  "items": [
    {
      "id": "0199fa10-3c4d-7e21-b6a0-9d8c7b6a5f40",
      "name": "Press Office",
      "description": "Media enquiries and announcements",
      "serviceAccountEmail": "press@andes.example",
      "avatarColor": "Mint",
      "memberCount": 4,
      "role": "TeamAdministrator"
    }
  ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 1
}
```

`role` is `null` for a team an `Administrator` can see but does not belong to. Use it to decide what to show: a member-management screen is worth showing when `role` is `TeamAdministrator`, or when the signed-in user is an `Administrator`. The API checks permissions on every call regardless, so this is a convenience, not a security boundary.

A caller with no teams gets an empty page, not an error.

### Create a team

```http
POST /api/teams
Content-Type: application/json

{
  "name": "Press Office",
  "description": "Media enquiries and announcements",
  "serviceAccountEmail": "press@andes.example",
  "avatarColor": "Seafoam"
}
```

The API trims the name, trims and lowercases the address, and looks the mailbox up in Microsoft Graph through `IDirectoryUserService`, the same lookup [adding a user](users-and-permissions.md#add-a-user) uses. A new team has **no members**; add them next, or nobody but an `Administrator` can work in it.

| Field | Rule |
| --- | --- |
| `name` | Required, at most 100 characters. |
| `description` | Optional, at most 500 characters. |
| `serviceAccountEmail` | Required, at most 256 characters, a valid address. |
| `avatarColor` | Optional, one of the six [colours](#team-settings). `Mint` when omitted. |

Every other setting starts at its default.

| Status | When |
| --- | --- |
| 201 | The team was created. |
| 400 | A field breaks its rule. |
| 403 | The caller lacks `CreateTeam`. |
| 404 | No directory account owns the service account's address. |
| 409 | An active team already has the name, or already sends from that mailbox, under any of its addresses. A request that loses a race with another create also returns 409. |

### Update a team

```http
PUT /api/teams/0199fa10-3c4d-7e21-b6a0-9d8c7b6a5f40
Content-Type: application/json

{
  "name": "Press and Media",
  "description": null,
  "serviceAccountEmail": "press@andes.example",
  "avatarColor": "Mint",
  "defaultSignature": "<p>Press Office</p>",
  "confirmRecipientsAbove": 50,
  "version": "AAAAAAAAB9E="
}
```

`PUT` **replaces** the details and every [setting](#team-settings), so send all of them each time. Only `description` and `defaultSignature` may be null. The API asks Graph again **only when the address changes**. Renaming a team therefore never depends on Graph being reachable.

**Who may change the mailbox.** A team administrator can save everything else, but the mailbox decides who the mail comes from, so changing `serviceAccountEmail` needs a **global** role that holds `ModifyTeam`. A team administrator who sends a different address gets a 403 ("Only an administrator can change the team's service mailbox.") and nothing is saved. Sending the current address is not a change.

The update applies only if the row still has the `version` you sent. If anyone changed the team in between, the API returns 409 and you read the team again before retrying. Besides the create rules, `version` must decode to 8 bytes, or the request is a 400.

| Status | When |
| --- | --- |
| 200 | The team was saved. The response holds the new `version`. |
| 400 | A field breaks its rule, or `version` is not an 8-byte base64 row version. |
| 403 | The caller lacks `ModifyTeam` in this team, or changes the service mailbox without a global role. |
| 404 | No active team has that id, the caller is not in it and holds no global role, or a new address has no directory account. |
| 409 | The `version` is stale, or another active team has the name or the mailbox, under any of its addresses. |

### Deactivate a team

`DELETE /api/teams/{teamId}` soft-deletes the team by stamping `DateDeactivated`. Nothing else is deleted. The membership and email rows stay, but they are hidden with the team: its members lose access and its emails disappear from every query. The name and the mailbox become free for another team.

| Status | When |
| --- | --- |
| 204 | The team was deactivated. |
| 403 | The caller lacks `DeactivateTeam`. |
| 404 | No active team has that id. |
| 409 | The team changed during the operation. |

There is no endpoint to reactivate a team yet. See [Known limitations](#known-limitations).

## Team members

All member routes sit under `/api/teams/{teamId}/members`. A member is addressed by the user's Entra object id, the same `id` as in [`/api/users`](users-and-permissions.md#the-user-object).

| Method and route | Permission | Success | Other statuses |
| --- | --- | --- | --- |
| `GET /api/teams/{teamId}/members` | `ViewTeam` | 200 with one page of members | 400, 403, 404 |
| `GET /api/teams/{teamId}/members/{userId}` | `ViewTeam` | 200 with the member | 403, 404 |
| `POST /api/teams/{teamId}/members` | `CreateTeamUser` | 201 with the member and a `Location` of `/api/teams/{teamId}/members/{userId}` | 400, 403, 404, 409 |
| `PUT /api/teams/{teamId}/members/{userId}` | `ModifyTeamUser` | 200 with the updated member | 400, 403, 404, 409 |
| `DELETE /api/teams/{teamId}/members/{userId}` | `DeactivateTeamUser` | 204 | 403, 404, 409 |
| `DELETE /api/teams/{teamId}/members/me` | `ViewTeam` | 204 | 404, 409 |

The four writes accept an optional `Idempotency-Key`; see [Idempotency](idempotency.md).

### The member object

```json
{
  "userId": "4ee09cb0-d7e7-4be4-b82f-2674695eb9df",
  "firstName": "Ana",
  "lastName": "Rojas",
  "fullName": "Ana Rojas",
  "email": "ana@andes.example",
  "role": "TeamMember",
  "dateCreated": "2026-11-15T19:52:30+00:00",
  "dateModified": null,
  "version": "AAAAAAAAB9F="
}
```

`version` is the **membership's** row version, not the user's or the team's. Send it back when you change the member's role.

`GET /api/teams/{teamId}/members` takes the same `page` and `pageSize` as the team list and orders members by last name, then first name.

### Add a member

```http
POST /api/teams/0199fa10-3c4d-7e21-b6a0-9d8c7b6a5f40/members
Content-Type: application/json

{ "email": "ana@andes.example", "role": "TeamMember" }
```

The person does not have to be a user yet. If no user has the address, the API looks the account up in Microsoft Graph and adds them as a user first, in the same call, so a `TeamAdministrator` can bring a colleague in without an `Administrator` stepping in. If the person is already an active user, the API uses that user as is.

| Field | Rule |
| --- | --- |
| `email` | Required, at most 256 characters, a valid address. |
| `role` | `TeamAdministrator` or `TeamMember`. `Administrator` is a 400, because it is not a team role. |

| Status | When |
| --- | --- |
| 201 | The member was added. |
| 400 | `email` is empty, too long, or not an address, or `role` is not a team role. |
| 403 | The caller lacks `CreateTeamUser` in this team. |
| 404 | The team is missing or the caller is not in it, or no directory account owns the address. |
| 409 | The person is already an active member, or another request added them at the same moment, or they are a user an `Administrator` deactivated, including one deactivated while the request ran. |

**Deactivated users.** Deactivating a user is an administrator's decision, so adding a member does not undo it. The call returns 409 until someone with `CreateUser` [adds the user again](users-and-permissions.md#add-a-user). If an `Administrator` deactivates the user while the request runs, the API removes the new membership again and returns 409 ("This user was deactivated while being added to the team."), so no active membership outlives its user.

**Returning members.** A person you removed earlier comes back as the same row with the role in the request, not a second row.

### Change a member's role

```http
PUT /api/teams/0199fa10-3c4d-7e21-b6a0-9d8c7b6a5f40/members/4ee09cb0-d7e7-4be4-b82f-2674695eb9df
Content-Type: application/json

{ "role": "TeamAdministrator", "version": "AAAAAAAAB9F=" }
```

The only thing a `PUT` changes is the role. Only this team's membership changes: the user's name, email, and roles in other teams are untouched. `role` follows the same rule as when adding; `version` is the membership's row version and must decode to 8 bytes.

| Status | When |
| --- | --- |
| 200 | The change was saved. The response holds the new `version`. |
| 400 | `role` is not a team role, or `version` is not an 8-byte base64 row version. |
| 403 | The caller lacks `ModifyTeamUser` in this team. |
| 404 | The team is missing or the caller is not in it, or the user is not an active member. |
| 409 | The change would leave the team without a team administrator, or the `version` is stale, or the membership or team changed during the operation. |

### Remove a member

`DELETE` soft-deletes the membership and nothing else. The user stays active and stays in their other teams. Removing the team's **last** `TeamAdministrator` returns 409; make another member a team administrator first.

| Status | When |
| --- | --- |
| 204 | The member was removed. |
| 403 | The caller lacks `DeactivateTeamUser` in this team. |
| 404 | The team is missing or the caller is not in it, or the user is not an active member. |
| 409 | The member is the team's last team administrator, or the membership or team changed during the operation. |

### Leave a team

`DELETE /api/teams/{teamId}/members/me` removes the **signed-in user** from the team. It takes no body and needs only `ViewTeam`, so any member can leave without asking an administrator. It soft-deletes the membership exactly as [removing a member](#remove-a-member) does, and the last-administrator rule applies the same way.

| Status | When |
| --- | --- |
| 204 | The caller left the team. |
| 404 | The team is missing, or the caller is **not a member of it**. A global `Administrator` who sees the team only through their role is not a member, so they get 404 too: there is nothing to leave. |
| 409 | The caller is the team's last team administrator, or the membership or team changed during the operation. |

`me` is a literal route segment, so it never collides with `{userId:guid}`.

### The last team administrator

A team should always have someone who can manage it, so the API refuses to demote, remove, or let leave the last `TeamAdministrator`. The check saves the team row together with the membership change. Because the team's `version` then changes, two requests that each demote a different administrator cannot both succeed: the second fails with 409 and its change is rolled back.

Two consequences follow:

- Demoting or removing a team administrator **also bumps the team's `version` and `dateModified`**. A team edit in flight at that moment gets a 409. Read the team again and retry.
- The rule has gaps. An `Administrator` deactivating a user, and a new team that has no members yet, can both leave a team without a team administrator. An `Administrator` can still manage such a team, because the role grants every permission in every team.

## How team access is checked

Routes under `/api/teams/{teamId}` run `TeamPermissionEndpointFilter` (`Andes.Mailer.Api/Filters/`) before request validation and the handler. It answers one question: may this caller do this in this team?

1. It reads the caller's `oid` claim and the `teamId` route value.
2. It asks `IPermissionService.CheckTeamPermissionAsync`, which looks at the caller's active membership first, then at their global roles.
3. It acts on the answer:

| Caller | Result |
| --- | --- |
| An active member whose team role grants the permission | Proceeds. |
| An active member whose team role does not grant it, and no global role does | 403, naming the permission. |
| Not a member, and no global role grants the permission | 404 "The team was not found." |
| A global `Administrator`, member or not | Proceeds, as long as the team is active. |
| Anyone, when the team is missing or deactivated | 404, even for an `Administrator`. |

The 404 for non-members is deliberate: the API never confirms that a team you cannot see exists, the same way it treats another team's emails. The 403 appears only for someone who is already in the team, where nothing is hidden. A deactivated team is out of reach even for an `Administrator`, so its emails cannot change while nobody can see the team.

`POST` and `DELETE` on `/api/teams` itself use the plain `PermissionEndpointFilter` instead, which checks only global roles: creating and deactivating teams belongs to the `Administrator` alone, whatever a member's team role. `PUT /api/teams/{teamId}` is a team route and uses `TeamPermissionEndpointFilter(ModifyTeam)`, so a team administrator can edit their own team; `TeamService` then keeps the mailbox with the global role. `GET /api/teams` needs no permission and filters its own results.

On the write routes, `IdempotencyEndpointFilter` runs **before** these permission filters, so a replay returns the caller's earlier success even when the permission check would now fail, for example after they left the team. See [Idempotency](idempotency.md#filter-order).

The check reads the database on every request, so a membership or role change takes effect on the caller's next call. Deactivating a user hides their memberships immediately; see [Deactivating a user](#deactivating-a-user).

To protect a new team route, put it under `/api/teams/{teamId:guid}` and add `.AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.X))`. The route value must be named `teamId`.

## The UI

The screens live in `andes-mailer-ui/src/app/features/`. Each is lazy-loaded, guarded by `permissionGuard`, and listed in `core/navigation/nav-items.ts`; see [Users and permissions](users-and-permissions.md#how-the-ui-uses-permissions). Every write sends an [idempotency key](idempotency.md#how-the-ui-sends-keys), and its button is disabled while the request runs.

| Page | Route | Needs | What it does |
| --- | --- | --- | --- |
| Team members | `/team/members` | `ModifyTeamUser` | Lists the active team's members with search and a role filter. **Add members** opens a dialog with directory search and a role (Member or Administrator); it adds several people at once and keeps any it failed to add selected, so a retry sends them again with the same keys. A role changes inline, with a confirmation when promoting. **Remove** asks first, then shows an Undo toast that adds the person back. Removing the last administrator opens an explanation instead. Tablets and phones get a card layout. |
| Team settings | `/team/settings` | `ModifyTeam` | A form (Angular Signal Forms) in three cards: General (name, avatar colour, description, and the mailbox, read-only unless you are a global `Administrator`), Sending defaults (a note on where replies go, then a rich-text signature editor), and Safety rules (the confirmation threshold). An unsaved-changes bar and a guard on leaving the page protect edits. If someone else saved first, a conflict alert offers **Reload**. Members see **Leave team**, which is disabled for the only administrator. |
| Teams | `/teams` | `CreateTeam` | Only a global `Administrator` sees it. Lists every team, creates one in a **New team** dialog (name, mailbox, description, colour), and deactivates one. After creating, it selects the new team and opens Team members. |

Team settings and Teams are separate because they answer different questions: settings are about **this** team and open to its administrators, while Teams is about the whole application.

The UI leaves out what the API does not have: a member's title and department, who added them, and a count of people in the organization.

## Deactivating a user

`DELETE /api/users/{id}` now also deactivates **every** team membership of that user, including those in deactivated teams, in the same save. Removing the memberships (and not only hiding them) means that reactivating a team later cannot bring back someone who was deactivated in the meantime. Adding the user again later starts them with no roles and no teams.

## Upgrading: the `AddTeams` migration

Apply it with:

```bash
dotnet ef database update --project Andes.Mailer.Repository --startup-project Andes.Mailer.Api
```

Run it from `andes-mailer-api/`. The migration changes data, not only the schema, and it cannot be undone:

- **It deletes every existing email, recipient, and attachment row.** Emails now need a team, and drafts made before teams existed have none to move to. The documents stay in the `attachments` container, orphaned. Clear the container by hand if they matter (locally, remove it from Azurite).
- **It removes global `TeamAdministrator` grants** from `[Core].[UserRole]`. That role is held per team now, and the new `CK_UserRole_RoleId` constraint would reject the rows. Re-grant access by adding those people to a team.
- It renames `[Core].[Email].[SenderId]` to `AuthorId`, adds `TeamId`, and adds the two team tables, the new `TeamMember` role, and permissions 8 to 12 with their grants.

`Down` restores the schema only. It cannot bring the deleted rows back, and it deletes any emails made since, because they belong to teams that no longer exist.

Afterwards the database has no teams. Sign in as the seeded administrator, create a team, and add its members. Until then, no one but an `Administrator` can reach any email route.

### The `AddTeamSettings` migration

This one is safe to apply to a database that has teams. It adds the settings columns with their defaults, creates and seeds `[Core.Ref].[TeamColor]`, and grants `ModifyTeam` to `TeamAdministrator`. Existing teams keep working and take the defaults. `Down` drops the columns, so any settings saved since are lost.

## Examples

### Set up a team

1. As an `Administrator`, create the team with `POST /api/teams`. Keep the `id` from the response.
2. Add the first team administrator: `POST /api/teams/{teamId}/members` with `"role": "TeamAdministrator"`.
3. That person (or the `Administrator`) adds everyone else with `"role": "TeamMember"`.
4. Members now compose drafts under `/api/teams/{teamId}/emails`.

### Read a 403 versus a 404

| You see | It means | The UI should |
| --- | --- | --- |
| 404 on `/api/teams/{teamId}/...` | The team does not exist, is deactivated, or you are not in it. | Treat it as "no such team". |
| 403 on `/api/teams/{teamId}/...` | You are in the team, but your role lacks the permission. | Hide or disable the action. |
| 409 on a member or team write | A stale `version`, or the last team administrator rule. | Read the resource again; if the message names the rule, tell the user. |

## Known limitations

- **The settings do nothing yet.** `confirmRecipientsAbove` and the signature are saved and returned, but no compose or send code reads them.
- **Replays assume one API instance.** Idempotency keys live in process memory; see [Idempotency](idempotency.md#limits).
- **No team reactivation.** Deactivating a team is one-way through the API. Its rows remain, so a developer can reactivate it by clearing `DateDeactivated` in SQL, but nothing in the API does it.
- **Teams can end up without an administrator.** Deactivating a user, or creating a team, can leave a team with no `TeamAdministrator`. Only the member endpoints enforce the rule.
- **Team administrator changes bump the team's version.** A concurrent team edit gets a 409; see [The last team administrator](#the-last-team-administrator).
- **A returning member keeps the original join date.** `dateCreated` is not reset when a removed member is added again.
- **Graph failures return 500.** A Graph outage, a missing consent, or a bad credential surfaces as a 500 from `POST /api/teams`, from `PUT` with a new address, and from `POST` on members, not as a specific error. The cause is in the API logs. See [Microsoft Graph setup](users-and-permissions.md#microsoft-graph-setup).
