# Email templates

Teams send the same kind of message again and again: a weekly status note to the same distribution list, an announcement with the same attached form. Retyping the subject, body, recipients, and documents each time wastes effort and invites mistakes. A template stores that starting point once, and any member of the team starts a draft from it in one call. This page covers the data model, the `/api/teams/{teamId}/templates` endpoints, how an email is made from a template, how a template is duplicated, and the migration that adds them. For the drafts a template produces, see [Emails and attachments](emails-and-attachments.md). For teams, see [Teams](teams.md). For who may do what, see [Users and permissions](users-and-permissions.md). For how tokens are issued and validated, see [Authentication](authentication.md).

## How it fits together

- A **template** belongs to a [team](teams.md). It holds a name, a subject, a sanitized HTML body, a list of recipients, and a list of attachments: everything a draft begins with.
- **Reading and using** templates is open to every member of the team. **Creating, changing, and deactivating** them, and managing their attachments, needs the new `ManageTeamTemplates` permission, which a `TeamAdministrator` holds and a `TeamMember` does not. A global `Administrator` holds it in every team.
- Starting an email from a template **copies** it. The draft has its own subject, body, recipients, and its own copy of every document. From then on it is an ordinary draft, and the template can change or disappear without touching it.
- A team administrator can **duplicate** a template into a new one, with its own copy of every document, to build a variation without starting over. It needs the `DuplicateTeamEmails` permission as well. See [Duplicate a template](#duplicate-a-template).
- A template's attachments use the same background upload as an email's: the request returns 202, the client polls, and a worker moves the file to Azure Blob Storage. See [The shared upload pipeline](#the-shared-upload-pipeline).

### Out of scope for now

These are not built, and the API makes no provision for them:

- The UI: there are no template screens yet. This change is API only.
- Reactivating a deactivated template.
- Templates shared across teams, and placeholders or merge fields in the body.
- Changing an email's template after the fact. `templateId` records where a draft came from and nothing more.

## Data model

Three tables live in the `Core` schema and arrive in the migration `Andes.Mailer.Repository/Sql/Migrations/*_AddEmailTemplates.cs`. The migration also adds a nullable `TemplateId` to `[Core].[Email]`.

| Table | Holds |
| --- | --- |
| `[Core].[EmailTemplate]` | One row per template |
| `[Core].[EmailTemplateRecipient]` | The addresses of a template, one row each |
| `[Core].[EmailTemplateAttachment]` | The documents of a template, one row each |

The lookup tables for recipient types and attachment statuses are the ones emails use; see [Data model](emails-and-attachments.md#data-model).

### `[Core].[EmailTemplate]`

| Column | Notes |
| --- | --- |
| `Id` | A GUID the API generates. |
| `TeamId` | The team the template belongs to, a foreign key to `[Core].[Team]`. Every query for templates filters on it. |
| `AuthorId` | The Entra object id of the user who created the template, a foreign key to `[Core].[User]`. It grants no special rights. |
| `Name` | Up to 128 characters, trimmed. Unique among the team's **active** templates only (a filtered unique index on `TeamId` and `Name` where `DateDeactivated IS NULL`), so a deactivated template frees its name. |
| `Subject` | Up to 255 characters, trimmed. May be empty. The same limit as an email's. |
| `BodyHtml` | The sanitized HTML body, `nvarchar(max)`. The request may carry up to 1,000,000 characters, the same limit and the same [sanitizer](emails-and-attachments.md#body-sanitization) as an email's. |
| `DateCreated`, `DateModified`, `DateDeactivated`, `Version` | The same audit, soft-delete, and `rowversion` columns as [`[Core].[User]`](users-and-permissions.md#coreuser). |

Subject, body, and recipients share the limits of an email, so any template fits the email made from it. The name limit is in `EmailTemplateLimits` (`Andes.Mailer.Common/Constants/Emails/`).

Deactivating the team hides its templates with it (the `TeamDeactivation` query filter). Deactivating a template hides its recipients and attachments (their filters follow the template's `DateDeactivated`), but keeps their rows and any stored documents.

### `[Core].[EmailTemplateRecipient]`

| Column | Notes |
| --- | --- |
| `TemplateId`, `Address` | The address is trimmed and stored in lowercase, up to 256 characters. A unique index on the pair means **one address appears once per template, across To, Cc, and Bcc**, the same rule as for an email. |
| `RecipientTypeId` | `To`, `Cc`, or `Bcc`. |
| `Position` | The address's index in the request's `recipients` array, so the entry order survives a reload. |

A template recipient stores **only the address and the header**. It keeps no directory user id and no display name, because a person's name can change after you save the template. The API resolves them through Microsoft Graph when an email is made from the template; see [Start an email from a template](#start-an-email-from-a-template). Saving a template therefore never calls Graph.

Like an email's recipients, a template's have no soft delete. A `PUT` without an address the template had before deletes that row for good.

### `[Core].[EmailTemplateAttachment]`

The same columns as [`[Core].[EmailAttachment]`](emails-and-attachments.md#coreemailattachment), with `TemplateId` in place of `EmailId`. Removing an attachment soft-deletes it.

| Column | Notes |
| --- | --- |
| `BlobName` | `templates/{templateId:N}/{attachmentId:N}`: both ids as 32 hex digits, under a `templates/` prefix in the same `attachments` container as email documents. The prefix means a template's blob name can never equal an email's. A duplicated template gets its own copy of each document under its own id. |

### `[Core].[Email].[TemplateId]`

A nullable foreign key to `[Core].[EmailTemplate]`. It is set when the draft is made from a template and null for a draft started from scratch. Deleting is restricted, which never matters in practice because templates are soft-deleted.

## Permissions

| Action | Permission in the team |
| --- | --- |
| List and read templates; read and download their attachments; start an email from a template | `ComposeTeamEmails` |
| Create, update, and deactivate a template | `ComposeTeamEmails` and `ManageTeamTemplates` |
| Upload and remove a template attachment | `ComposeTeamEmails`, `ManageTeamTemplates`, and `CanAttachDocuments` |
| Duplicate a template | `ComposeTeamEmails`, `ManageTeamTemplates`, and `DuplicateTeamEmails` |

`ManageTeamTemplates` (13) is described as "Creates, changes, and deactivates a team's email templates". `TeamAdministrator` and the global `Administrator` hold it; `TeamMember` does not. Both team roles hold `ComposeTeamEmails`, so every member can read and use templates. The grants are in [Users and permissions](users-and-permissions.md#roles-and-permissions).

`DuplicateTeamEmails` (14) is described as "Duplicates a team's emails and email templates" and is held by every role. Duplicating a template creates one, so the route also needs `ManageTeamTemplates`: a `TeamMember` holds the first but not the second and gets a 403, while a `TeamAdministrator` and the global `Administrator` pass. Duplicating an *email* needs only `DuplicateTeamEmails`; see [Duplicate an email](emails-and-attachments.md#duplicate-an-email).

Both the template group and `from-template` run the `ComposeTeamEmails` team check first (see [How team access is checked](teams.md#how-team-access-is-checked)). The consequences:

- A caller who is not in the team, or a team that is missing or deactivated, gets 404. A template in another team returns the same 404 as one that does not exist.
- A `TeamMember` who calls a write route, including `duplicate`, gets 403, naming the missing permission. The team check ran first, so that member is already in the team and nothing is hidden.
- A global `Administrator` passes every check for every active team.

## Endpoints

All routes need a token with the `access_as_user` scope, like every other endpoint. Errors are Problem Details responses. Enums travel by **name**, and property names are camelCase.

### Templates

| Method and route | Permission (besides `ComposeTeamEmails`) | Success | Other statuses |
| --- | --- | --- | --- |
| `GET /api/teams/{teamId}/templates` | None | 200 with one page of the team's templates, ordered by name | 400 for a bad `page` or `pageSize`; 403; 404 |
| `GET /api/teams/{teamId}/templates/{id}` | None | 200 with the template, its recipients, and its attachments | 403; 404 |
| `POST /api/teams/{teamId}/templates` | `ManageTeamTemplates` | 201 with the template and a `Location` of `/api/teams/{teamId}/templates/{id}` | 400; 403; 404; 409 if an active template of the team has the name |
| `PUT /api/teams/{teamId}/templates/{id}` | `ManageTeamTemplates` | 200 with the updated template | 400; 403; 404; 409 if the `version` is stale or another active template has the name |
| `POST /api/teams/{teamId}/templates/{id}/duplicate` | `ManageTeamTemplates` and `DuplicateTeamEmails` | 201 with the new template and a `Location` of `/api/teams/{teamId}/templates/{id}` | 400 for a missing body or a blank or too-long name; 403; 404; 409 if the name is taken or an attachment is still uploading |
| `DELETE /api/teams/{teamId}/templates/{id}` | `ManageTeamTemplates` | 204 | 403; 404; 409 if another request changed the template at the same moment |

Listing takes the same `page` (default 1) and `pageSize` (default 25, at most 100) as [`GET /api/users`](users-and-permissions.md#list-users) and returns the same wrapper. Ties in name are broken by id.

### The template object

```json
{
  "id": "0199fb42-6e0a-7a3d-8f12-4b5c6d7e8f90",
  "authorId": "4ee09cb0-d7e7-4be4-b82f-2674695eb9df",
  "name": "Weekly status",
  "subject": "Weekly status",
  "bodyHtml": "<p>Hello <strong>team</strong>,</p>",
  "recipients": [
    { "address": "ana@andes.example", "type": "To" },
    { "address": "press@outside.example", "type": "Bcc" }
  ],
  "attachments": [],
  "dateCreated": "2026-11-16T15:20:44+00:00",
  "dateModified": null,
  "version": "AAAAAAAAB9E="
}
```

`recipients` are ordered by header (To, Cc, Bcc), then by position. `attachments` use the same [attachment object](emails-and-attachments.md#the-attachment-object) as an email's, oldest first. `version` is the base64 row version; send it back unchanged when you update the template.

The list returns a summary of each template instead: `id`, `name`, `subject`, `recipientCount`, `attachmentCount`, `dateCreated`, and `dateModified`. `attachmentCount` includes attachments still uploading and ones that failed.

### Create and update a template

```http
POST /api/teams/0199fa10-3c4d-7e21-b6a0-9d8c7b6a5f40/templates
Content-Type: application/json

{
  "name": "Weekly status",
  "subject": "Weekly status",
  "bodyHtml": "<p>Hello <strong>team</strong>,</p>",
  "recipients": [
    { "address": "ana@andes.example", "type": "To" },
    { "address": "press@outside.example", "type": "Bcc" }
  ]
}
```

`PUT /api/teams/{teamId}/templates/{id}` takes the same fields plus the `version` you last read. It **replaces** the name, subject, body, and recipients: send the full list each time, because an address you leave out is removed. It does not touch attachments, which have their own routes.

The rules, for both:

| Field | Rule |
| --- | --- |
| `name` | Required and not blank, at most 128 characters, trimmed. Unique among the team's active templates. |
| `subject` | Required (may be empty), at most 255 characters. |
| `bodyHtml` | Required (may be empty), at most 1,000,000 characters before sanitizing. |
| `recipients` | Required, at most 500, no null entries, no repeated address (case-insensitive, any header). |
| `recipients[].address` | A single plain address of at most 256 characters: no display name, no spaces. |
| `recipients[].type` | `To`, `Cc`, or `Bcc`. |
| `version` (update only) | The base64 row version from the last response, decoding to 8 bytes. |

The update applies only if the row still has the `version` you sent. If anyone changed the template in between, the API returns 409 and you read the template again before retrying. A change to the recipients alone still bumps the version. Attachment changes do not touch the template's version, so an upload finishing never causes a conflict.

A name clash is a 409 with the message "The team already has a template with this name." Two requests that race for one name cannot both win: the unique index rejects the loser, and the API reports it as the same 409.

### Deactivate a template

`DELETE /api/teams/{teamId}/templates/{id}` soft-deletes the template by stamping `DateDeactivated`. Its name becomes free for another template. The template, its recipients, and its attachments disappear from every query, and **emails already made from it are untouched**: they keep their content and their own copies of the documents, and their `templateId` still names the deactivated template.

The documents of a deactivated template stay in the `attachments` container, the same way [deleting an email](emails-and-attachments.md#known-limitations) leaves its documents. Remove the template's attachments first if the files must go.

## Duplicate a template

A variation on a template, such as the same weekly note for another list, is quicker to make from a copy than from scratch. Duplicating creates a new template from an existing one.

```http
POST /api/teams/0199fa10-3c4d-7e21-b6a0-9d8c7b6a5f40/templates/0199fb42-6e0a-7a3d-8f12-4b5c6d7e8f90/duplicate
Content-Type: application/json

{ "name": "Weekly status (north)" }
```

The route needs `ComposeTeamEmails`, `ManageTeamTemplates`, and `DuplicateTeamEmails`, so a plain `TeamMember` gets a 403 (see [Permissions](#permissions)). It returns 201 with an [`EmailTemplateDto`](#the-template-object) and a `Location` of `/api/teams/{teamId}/templates/{id}`.

The request needs a body, but `{}` is enough. A missing body is a 400.

| Field | Rule |
| --- | --- |
| `name` | Optional. When given, it is trimmed and follows the [name rule](#create-and-update-a-template): not blank, at most 128 characters, unique among the team's active templates. When omitted or `null`, the API picks one. |

**Choosing the name.** Without a `name`, the copy is called `"{Name} (copy)"`. If the team already has that, it tries `"{Name} (copy 2)"`, then `"{Name} (copy 3)"`, and so on until a name is free. Names are compared ignoring case, as the database does. The source name is cut when needed, so the copy's name always fits in 128 characters. A name you give that is already taken is a 409, not a new suffix: the API does not guess when you chose.

What the new template gets:

| Part | What happens |
| --- | --- |
| Author | The caller is the `authorId`. |
| Subject and body | Copied as stored. |
| Recipients | Copied with the same header and order. A template keeps addresses only, so there is nothing to resolve and no Graph call. |
| Attachments | Each `Uploaded` attachment is copied inside storage to a new attachment row with status `Uploaded`, the blob name `templates/{templateId:N}/{attachmentId:N}`, and the source's `ContentHash`, so later uploads to the copy are checked against these documents. `Failed` attachments are left out. The copies are ready at once, so there is nothing to poll. |

The copy is independent: later changes to either template never reach the other, and removing a document from one never deletes the other's.

| Status | When |
| --- | --- |
| 201 | The template was created. |
| 400 | The body is missing, or `name` is blank or longer than 128 characters. |
| 403 | The caller lacks `ManageTeamTemplates` or `DuplicateTeamEmails` in the team, as a `TeamMember` does. |
| 404 | The team is missing or the caller is not in it, or the team has no active template with the id (another team's template gives the same answer), or storage no longer holds one of the template's documents. |
| 409 | The name is taken ("The team already has a template with this name."), or one of the source's attachments is `Pending` or `Uploading` (`TemplateNotReadyException`, the same message as for [from-template](#when-a-template-is-not-ready)). |

Copying uses the order and cleanup rules in [When the save fails](#when-the-save-fails): the documents are copied first, then the template and its rows are saved together, and the copies are deleted if anything fails before the save commits. The code is shared with [duplicating an email](emails-and-attachments.md#duplicate-an-email) and with `from-template` (`AttachmentCopy`, in `Andes.Mailer.Service/Emails/Attachments/`).

The permission arrives in the migration `*_AddDuplicateTeamEmailsPermission.cs`, which only adds the permission and its grants. Apply it as described in [Run it locally](#run-it-locally).

## Template attachments

Documents go in under `/api/teams/{teamId}/templates/{templateId}/attachments`. The routes mirror [an email's attachment routes](emails-and-attachments.md#endpoints-1).

| Method and route | Permission (besides `ComposeTeamEmails`) | Success | Other statuses |
| --- | --- | --- | --- |
| `POST /api/teams/{teamId}/templates/{templateId}/attachments` | `ManageTeamTemplates` and `CanAttachDocuments` | 202 with the attachment and a `Location` of the new attachment | 400; 403; 404; 409 if the template already has the same document attached |
| `GET /api/teams/{teamId}/templates/{templateId}/attachments/{id}` | None | 200 with the attachment | 403; 404 |
| `GET /api/teams/{teamId}/templates/{templateId}/attachments/{id}/content` | None | 200 with the file | 403; 404; 409 until the status is `Uploaded` |
| `DELETE /api/teams/{teamId}/templates/{templateId}/attachments/{id}` | `ManageTeamTemplates` and `CanAttachDocuments` | 204 | 403; 404 |

The upload flow is the one in [Upload flow](emails-and-attachments.md#upload-flow): send the file as `multipart/form-data` in a field named `file`, read the 202, then poll the attachment until `status` is `Uploaded` or `Failed`. A `Failed` attachment carries a `failureReason`; remove it and upload the file again.

A template refuses a document it already holds, whatever the file is called. The upload is a 409 with "This document is already attached to the template as Q3 report.pdf." and nothing is staged or saved. The comparison is the SHA-256 of the bytes against the template's `Pending`, `Uploading`, and `Uploaded` attachments; `Failed` and removed ones do not block, two different files with the same name are both accepted, and the same document on another template or email is accepted. Rows saved before the migration `AddAttachmentContentHash` have no hash and are never matched. See [Duplicate documents](emails-and-attachments.md#duplicate-documents).

Removing a template attachment deletes its document from storage, but **never touches emails already made from the template**: each of those holds its own copy of the document under its own blob name.

### Validation

Template attachments follow the rules in [Validation](emails-and-attachments.md#validation) without change: the same file types, the same check of the file's leading bytes, at most 25 MB per file, and at most **25 MB per template** in total (`Failed` attachments do not count). A 400 error for the total names a "template" instead of an "email". The same limits mean a template that fits always fits the email made from it. As for emails, parallel uploads to one template cannot each pass the cap, because the size sum and the insert run under a lock per template.

## Start an email from a template

```http
POST /api/teams/0199fa10-3c4d-7e21-b6a0-9d8c7b6a5f40/emails/from-template
Content-Type: application/json

{ "templateId": "0199fb42-6e0a-7a3d-8f12-4b5c6d7e8f90" }
```

The route needs only `ComposeTeamEmails`, so every member of the team can use it, exactly as they can start a draft from scratch. It returns 201 with an [`EmailDto`](emails-and-attachments.md#the-email-object) and a `Location` of `/api/teams/{teamId}/emails/{id}`.

What the new draft gets:

| Part | What happens |
| --- | --- |
| Author and status | The caller is the `authorId`; the status is `Draft`. |
| `templateId` | Set to the template's id. |
| Subject and body | Copied as stored. They were sanitized and trimmed when the template was saved. |
| Recipients | Copied, with the same header and order. The API **re-resolves each address through Microsoft Graph**, so `userId` and `displayName` reflect the directory today, as for any new draft. See [Recipient resolution](emails-and-attachments.md#recipient-resolution). |
| Attachments | Each `Uploaded` template attachment is copied to a new attachment row with status `Uploaded`, the blob name `{emailId:N}/{attachmentId:N}`, and the template attachment's `ContentHash`, so later uploads to the draft are checked against these documents. The document is copied inside storage, not through the API. |

`Failed` attachments are skipped, because they hold no document. The client has no extra work to do afterwards: the copies are already `Uploaded`, so there is nothing to poll.

| Status | When |
| --- | --- |
| 201 | The draft was created. |
| 400 | `templateId` is missing or empty. |
| 403 | Not returned today. The caller's team role would need to lack `ComposeTeamEmails`, which both team roles hold. |
| 404 | The team is missing or the caller is not in it, or the team has no active template with the id (another team's template gives the same answer), or storage no longer holds one of the template's documents. |
| 409 | One of the template's attachments is still `Pending` or `Uploading`. |

### When a template is not ready

If any attachment is `Pending` or `Uploading`, the call returns 409 (`TemplateNotReadyException`) with "The template's attachments are still uploading. Try again once every attachment is Uploaded or Failed." Nothing is created. Wait until the template's attachments settle, then call again. A template with only `Uploaded` and `Failed` attachments is ready.

### When a document is missing from storage

If the row says `Uploaded` but the blob is gone, for example because someone cleared the container, the call returns 404 with "One of the template's documents is no longer in storage. Remove it from the template and try again." No email is saved, and the API deletes any copies it already made (best effort; a failed cleanup is logged, and the leftover blob is harmless). Remove the dead attachment from the template, then try again.

### When the save fails

The copies exist before the email row does, so the order matters:

1. The API reads the template and checks that it is ready.
2. It resolves the recipients through Graph. A Graph failure surfaces as a 500, with nothing copied.
3. It copies each document, then saves the email and its attachment rows in one save.
4. If anything fails before the save commits, it deletes the copies.
5. If the save fails **after its commit landed** (a lost acknowledgement, or a cancellation at the last moment), the draft exists, so the API keeps its copies. When it cannot tell whether the save landed, it also keeps them, because an orphaned blob costs less than a draft with missing documents.

### After the draft exists

The draft is an ordinary draft. Edit, update, delete, and attach documents to it through the [existing email endpoints](emails-and-attachments.md#endpoints). Later template edits, attachment changes, and deactivation never change it. `templateId` stays on the email and on the list summaries, so a UI can show where a draft came from.

## The shared upload pipeline

Template attachments reuse the pipeline described in [The background pipeline](emails-and-attachments.md#the-background-pipeline). There is one staging directory, one bounded channel, one set of `MaxConcurrency` workers, one retry policy, and one [startup recovery](emails-and-attachments.md#startup-recovery) for both kinds of attachment. The `AttachmentUpload` settings apply to both.

For maintainers:

- `AttachmentLifecycle` (`Andes.Mailer.Service/Emails/Attachments/`) holds the logic both owners share: validate and stage a file, serve a stored document, and remove an attachment. `AttachmentService` and `TemplateAttachmentService` call it and add only what is specific to their owner, such as which row to look up and how to sum the bytes already used.
- The channel carries `StagedAttachmentJob(AttachmentOwners Owner, Guid Id)`. `AttachmentOwners` (`Email` or `Template`) tells the worker which table holds the row. The attachment id still names the staged file.
- `IAttachmentStore.CopyAsync(source, destination)` copies a blob with Copy Blob inside one storage account and waits for it to finish. It needs no shared access signature, because the client's own credential authorizes it, and the document never passes through the API. Only a 404 maps to `BlobNotFoundException`; any other failure, including a 403 on the source, propagates as a fault.
- Both attachment entities implement `IStagedAttachment`, so the worker and recovery treat them alike.

## Run it locally

Templates need the same setup as emails: a database with the migration applied and a blob store. See [Run it locally](emails-and-attachments.md#run-it-locally) for Azurite and the connection strings.

From `andes-mailer-api/`, apply the migration:

```bash
dotnet ef database update --project Andes.Mailer.Repository --startup-project Andes.Mailer.Api
```

Then, as a team administrator or the seeded `Administrator`, create a template with `POST /api/teams/{teamId}/templates`, upload a document to it, poll until it is `Uploaded`, and call `POST /api/teams/{teamId}/emails/from-template`.

## Upgrading: the `AddEmailTemplates` migration

The migration only adds. It deletes no data and removes no grants.

- It adds `[Core].[EmailTemplate]`, `[Core].[EmailTemplateRecipient]`, and `[Core].[EmailTemplateAttachment]`, with the filtered unique index `IX_EmailTemplate_TeamId_Name`.
- It adds the nullable column `TemplateId` and its foreign key and index to `[Core].[Email]`. Existing emails get `null`.
- It seeds the permission `ManageTeamTemplates` (13) and grants it to `Administrator` and `TeamAdministrator`.

Apply it with the command above. No data needs moving. The new permission takes effect on the next request of every `TeamAdministrator`; there is nothing to refresh in the token. `Down` drops the three tables, the column, and the permission with its grants, so it deletes any templates made since.

## Upgrading: the `AddAttachmentContentHash` migration

The migration only adds. It puts the nullable column `ContentHash binary(32)` on `[Core].[EmailTemplateAttachment]` and on `[Core].[EmailAttachment]` and deletes no data. It does not backfill, so documents attached before it are not compared with new uploads; remove and upload one again if it must be protected. `Down` drops both columns. Apply it with the command above.

## Examples

### Set up and use a template

1. As a team administrator, create the template with `POST /api/teams/{teamId}/templates`. Keep the `id`.
2. Upload its documents with `POST .../templates/{id}/attachments` and poll each until `Uploaded`.
3. Any member starts a draft with `POST /api/teams/{teamId}/emails/from-template`.
4. The member edits the draft with `PUT /api/teams/{teamId}/emails/{id}` and, later, sends it once sending exists.

### Read the status codes

| You see | It means | The UI should |
| --- | --- | --- |
| 404 on a template route | The team or template does not exist, is deactivated, or you are not in the team. | Treat it as "no such template". |
| 403 on a template write | You are in the team, but your role lacks `ManageTeamTemplates` (or `CanAttachDocuments`). | Hide the edit actions for a `TeamMember`. |
| 409 on a template attachment `POST` | The template already holds the same document; the message names the file. | Show the message; the document is already there, so there is nothing to retry. |
| 409 on `POST` or `PUT` | The name is taken, or the `version` is stale. | Read the template again; if the message names the name, ask for another. |
| 409 on `from-template` | A template attachment is still uploading. | Say so, and offer to retry shortly. |
| 409 on `duplicate` | The name you gave is taken, or an attachment is still uploading. | Read the message; ask for another name, or retry shortly. |
| 403 on `duplicate` | You are in the team, but your role lacks `ManageTeamTemplates`. | Hide the duplicate action for a `TeamMember`. |
| 404 on `from-template` | The template is gone, or one of its documents is missing from storage. | Read the message; a missing document is for a team administrator to fix. |

## Known limitations

- **No UI.** The UI has no template screens yet.
- **No template reactivation.** Deactivating a template is one-way through the API. Its rows remain, so a developer can clear `DateDeactivated` in SQL, but nothing in the API does it.
- **Deactivating a template leaves its documents in storage.** Remove its attachments first if the files must go.
- **A leftover copy can stay in storage.** If an email made from a template, or a duplicated template, fails and the cleanup of its copies also fails, or the API cannot tell whether the save landed, the copies stay in the `attachments` container.
- **`from-template` and `duplicate` are not instant for large templates.** They wait for each document to be copied, one after another, before they answer.
- **Recipient names come from the moment of copying.** The template stores addresses only; the draft's `userId` and `displayName` are set when it is created and do not refresh, as with any [draft](emails-and-attachments.md#known-limitations).
- **Graph failures return 500.** A Graph outage surfaces as a 500 from `from-template`, because the recipients are resolved then. Saving a template does not call Graph.
- **A single instance only.** The upload pipeline is shared, so it has the same [scaling limit](emails-and-attachments.md#scaling-out).
- **No malware scanning.** The same content check applies; treat a download as untrusted.
