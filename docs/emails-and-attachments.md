# Emails and attachments

Andes Mailer will send email on behalf of teams, and a message takes work to prepare: a subject, a formatted body, recipients, and documents. The API stores that work as a **draft** so the team can build it up over several requests, leave, and come back. This page covers the draft data model, the `/api/teams/{teamId}/emails` endpoints, how attachment uploads work end to end, and the configuration and local setup that uploads need. For teams and who may do what in them, see [Teams](teams.md) and [Users and permissions](users-and-permissions.md). For how tokens are issued and validated, see [Authentication](authentication.md).

## How it fits together

- An **email** is a draft that belongs to a [team](teams.md). Every member of the team can view, edit, attach documents to, and delete any of the team's drafts, whoever started it. To anyone outside the team it does not exist. A global `Administrator` can work in every team's emails.
- The email records its **author**, the team member who started the draft, so the UI can show who it was. The author gets no special rights.
- The team's service account is the email's sender. Nothing sends yet, so this matters later; see [Teams](teams.md#how-it-fits-together).
- A **recipient** is an address under the To, Cc, or Bcc header. The API asks Microsoft Graph who owns each new address, so the UI can later show names for people in your organization. Any other address is kept as plain text.
- An **attachment** is a document added to a draft. The request that receives it returns at once; a background job copies the file to Azure Blob Storage, and the client polls for the result. This keeps a slow upload to storage from holding the request open or failing the user's save.
- A draft never leaves the `Draft` status yet. `Sent` exists in the model so a later feature can use it without a migration.

### Out of scope for now

These are not built, and the API makes no provision for them:

- The UI: the TipTap compose screen, the recipient picker, and upload progress.
- A directory typeahead endpoint for the picker.
- Sending a draft through Microsoft Graph.
- Inline images in the body (the sanitizer removes `img`; see [Body sanitization](#body-sanitization)).
- Running more than one API instance (see [Scaling out](#scaling-out)).
- Malware scanning of uploads.

## Data model

Application tables live in `Core`; fixed lookup data lives in `Core.Ref`. All of them arrive in the migration `Andes.Mailer.Repository/Sql/Migrations/*_AddEmailsAndAttachments.cs`. The later migration `*_AddTeams.cs` gives each email a team and an author, and **deletes every email that existed before it**; see [Upgrading](teams.md#upgrading-the-addteams-migration).

| Table | Holds |
| --- | --- |
| `[Core].[Email]` | One row per draft |
| `[Core].[EmailRecipient]` | The addresses of an email, one row each |
| `[Core].[EmailAttachment]` | The documents of an email, one row each |
| `[Core.Ref].[DocumentType]` | The accepted file types (`DocumentTypes`) |
| `[Core.Ref].[RecipientType]` | `To`, `Cc`, `Bcc` (`RecipientTypes`) |
| `[Core.Ref].[EmailStatus]` | `Draft`, `Sent` (`EmailStatuses`) |
| `[Core.Ref].[AttachmentStatus]` | `Pending`, `Uploading`, `Uploaded`, `Failed` (`AttachmentStatuses`) |

The enums are in `Andes.Mailer.Common/Enums/`. Like the roles and permissions, the lookup rows belong to the code, not to operators.

### `[Core].[Email]`

| Column | Notes |
| --- | --- |
| `Id` | A GUID the API generates. |
| `TeamId` | The team the email belongs to, a foreign key to `[Core].[Team]`. Every query for emails filters on it. |
| `AuthorId` | The Entra object id of the team member who started the draft, a foreign key to `[Core].[User]`. Renamed from `SenderId`; it no longer limits who can see or change the email. |
| `Subject` | Up to 255 characters, trimmed. May be empty. |
| `BodyHtml` | The sanitized HTML body, `nvarchar(max)`. The request may carry up to 1,000,000 characters. |
| `StatusId` | `Draft` today. `Sent` is reserved until sending is built. |
| `DateCreated`, `DateModified`, `DateDeactivated`, `Version` | The same audit, soft-delete, and `rowversion` columns as [`[Core].[User]`](users-and-permissions.md#coreuser). |

Deactivating the team hides its emails with it (the `TeamDeactivation` query filter), without touching their rows. Deleting an email soft-deletes it. Its recipients and attachments disappear from queries with it (their query filters follow the email's `DateDeactivated`), but their rows and any stored documents stay; see [Known limitations](#known-limitations).

### `[Core].[EmailRecipient]`

| Column | Notes |
| --- | --- |
| `EmailId`, `Address` | The address is trimmed and stored in lowercase, up to 256 characters. A unique index on the pair means **one address appears once per email, across To, Cc, and Bcc**. A request that repeats an address, even under a different header or with different casing, is a 400. |
| `RecipientTypeId` | `To`, `Cc`, or `Bcc`. |
| `Position` | The address's index in the request's `recipients` array, so the entry order survives a reload. Responses sort by header, then position. |
| `UserId` | The Entra object id when Graph found an account that owns the address; null for an external address. It is not a foreign key: a recipient can be anyone in the directory, not only a user of this application. |
| `DisplayName` | The account's full name, cut to 256 characters; null for an external address. |

A recipient has no soft delete. When you save a draft without an address it had before, the API deletes that row for good. Addresses you keep are updated in place (their header and position) without a new directory lookup.

### `[Core].[EmailAttachment]`

| Column | Notes |
| --- | --- |
| `EmailId` | The draft the file belongs to. |
| `FileName` | The client's name with any folder path removed, up to 255 characters. |
| `DocumentTypeId`, `ContentType` | Taken from the extension. The media type is what a download is served with. |
| `SizeBytes` | The bytes that reached the server's disk, not the size the request declared. |
| `BlobName` | `{emailId:N}/{attachmentId:N}`: both ids as 32 hex digits, so a draft's documents share a prefix. |
| `StatusId`, `FailureReason` | Where the upload stands. A failure carries a short message to show the user. |
| `DateCreated`, `DateModified`, `DateDeactivated`, `Version` | Removing an attachment soft-deletes it. |

## Endpoints

All routes need a token with the `access_as_user` scope, like every other endpoint. Errors are Problem Details responses. Enums travel by **name** in both directions (`"Cc"`, `"Pending"`), never by number, so a client does not depend on the stored values. Property names are camelCase.

Every email route sits under `/api/teams/{teamId}`, and a check on the team runs first (see [How team access is checked](teams.md#how-team-access-is-checked)). It needs the `ComposeTeamEmails` permission **in that team**:

- A caller who is not in the team, or a team that is missing or deactivated, gets 404. An email that exists in another team returns the same 404 as one that does not exist, so the API never confirms that another team's draft is there.
- A caller who is in the team but whose role lacks the permission gets 403. Both team roles hold it today, so in practice this does not happen yet.
- A global `Administrator` passes the check for every active team, member or not.

The routes moved from `/api/emails` to `/api/teams/{teamId}/emails`. **This is a breaking change**: the old routes no longer exist.

### Emails

| Method and route | Success | Other statuses |
| --- | --- | --- |
| `GET /api/teams/{teamId}/emails` | 200 with one page of the team's emails, most recently changed first | 400 for a bad `page` or `pageSize`; 403; 404 |
| `GET /api/teams/{teamId}/emails/{id}` | 200 with the email, its recipients, and its attachments | 403; 404 |
| `POST /api/teams/{teamId}/emails` | 201 with the email and a `Location` of `/api/teams/{teamId}/emails/{id}` | 400; 403; 404 |
| `PUT /api/teams/{teamId}/emails/{id}` | 200 with the updated email | 400; 403; 404; 409 if the `version` is stale or the email is no longer a draft |
| `DELETE /api/teams/{teamId}/emails/{id}` | 204 | 403; 404; 409 if another request changed the email at the same moment |

The signed-in user becomes the email's `authorId` on create. Any member of the team can then edit or delete the draft, not only its author. Listing takes the same `page` (default 1) and `pageSize` (default 25, at most 100) as [`GET /api/users`](users-and-permissions.md#list-users) and returns the same wrapper.

### The email object

```json
{
  "id": "0199f7a2-5d1e-7c3b-9a44-1f2e3d4c5b6a",
  "authorId": "4ee09cb0-d7e7-4be4-b82f-2674695eb9df",
  "subject": "Quarterly report",
  "bodyHtml": "<p>Hello <strong>team</strong>,</p>",
  "status": "Draft",
  "recipients": [
    { "address": "ana@andes.example", "type": "To", "userId": "4ee09cb0-d7e7-4be4-b82f-2674695eb9df", "displayName": "Ana Rojas" },
    { "address": "press@outside.example", "type": "Bcc", "userId": null, "displayName": null }
  ],
  "attachments": [],
  "dateCreated": "2026-11-14T18:02:11+00:00",
  "dateModified": null,
  "version": "AAAAAAAAB9E="
}
```

`authorId` is the Entra object id of the team member who started the draft. Look the name up in [the team's members](teams.md#team-members); a member who has since left the team still has a user row, but no longer appears in that list.

The list returns a summary of each email instead: `id`, `authorId`, `subject`, `status`, `recipientCount`, `attachmentCount`, `dateCreated`, and `dateModified`.

### Create and update a draft

```http
POST /api/teams/0199fa10-3c4d-7e21-b6a0-9d8c7b6a5f40/emails
Content-Type: application/json

{
  "subject": "Quarterly report",
  "bodyHtml": "<p>Hello <strong>team</strong>,</p>",
  "recipients": [
    { "address": "ana@andes.example", "type": "To" },
    { "address": "press@outside.example", "type": "Bcc" }
  ]
}
```

`PUT /api/teams/{teamId}/emails/{id}` takes the same fields plus the `version` you last read. It **replaces** the subject, body, and recipients: send the full list each time, because an address you leave out is removed. It does not touch attachments, which have their own routes.

The rules, for both:

| Field | Rule |
| --- | --- |
| `subject` | Required (may be empty), at most 255 characters. |
| `bodyHtml` | Required (may be empty), at most 1,000,000 characters before sanitizing. |
| `recipients` | Required, at most 500, no null entries, no repeated address (case-insensitive, any header). |
| `recipients[].address` | A single plain address of at most 256 characters: no display name, no spaces. |
| `recipients[].type` | `To`, `Cc`, or `Bcc`. |
| `version` (update only) | The base64 row version from the last response, decoding to 8 bytes. |

The update applies only if the row still has the `version` you sent. If anyone changed the email in between, such as a second tab or another member of the team, the API returns 409 and you read the email again before retrying. Without this, two editors would silently overwrite each other. Attachment changes do not touch the email's version, so an upload finishing never causes a conflict. A `Sent` email is also a 409: only a draft can change.

### Body sanitization

The server cleans `bodyHtml` before it stores it, so a saved body is safe to render when the draft reloads, whatever the client sent. The allowlist matches what the compose editor (TipTap StarterKit, Underline, Link, TextAlign) produces. The code is `Andes.Mailer.Service/Emails/EmailBodySanitizer.cs`.

| Kept | Details |
| --- | --- |
| Elements | `p`, `br`, `hr`, `h1` to `h6`, `strong`, `b`, `em`, `i`, `u`, `s`, `strike`, `code`, `pre`, `blockquote`, `ul`, `ol`, `li`, `a`, `span` |
| Attributes | `href`, `target`, `rel`, `start`, `style` (no `data-*`) |
| CSS | `text-align` only |
| URL schemes | `http`, `https`, `mailto` |

Everything else is removed without an error, including scripts, event handlers, frames, and `img`. The response shows the cleaned body, so a client that sent something outside the list sees it gone. Inline images are not supported; a document goes in as an attachment.

### Recipient resolution

When you create or update a draft, the API looks up each **new** address through `IDirectoryUserService.FindByEmailsAsync`, which calls Microsoft Graph as the application (the same app-only permission and credential as [adding a user](users-and-permissions.md#microsoft-graph-setup)). An address matches an account whose `mail` or `userPrincipalName` equals it. A match fills `userId` and `displayName`; no match leaves them null.

Graph allows only 15 expressions in a directory filter, so the lookup sends at most 7 addresses per request, and the filter uses each list twice (`mail in (...) or userPrincipalName in (...)`). A draft with 500 new addresses makes about 72 requests. Addresses already on the draft are not looked up again, and a draft with no new addresses makes no call. A Graph failure surfaces as a 500 from the save, with the cause in the logs.

The lookup happens at save time. An address that had no account when you added it keeps `userId` null until you remove it and add it again.

## Attachments

Documents go in as attachments, under `/api/teams/{teamId}/emails/{emailId}/attachments`. The upload is the one place the API does work after it has answered, so it is the part worth understanding before you build on it.

### Endpoints

| Method and route | Permission in the team | Success | Other statuses |
| --- | --- | --- | --- |
| `POST /api/teams/{teamId}/emails/{emailId}/attachments` | `ComposeTeamEmails` and `CanAttachDocuments` | 202 with the attachment and a `Location` of the new attachment | 400, 403, 404, 409 |
| `GET /api/teams/{teamId}/emails/{emailId}/attachments/{id}` | `ComposeTeamEmails` | 200 with the attachment | 403, 404 |
| `GET /api/teams/{teamId}/emails/{emailId}/attachments/{id}/content` | `ComposeTeamEmails` | 200 with the file | 403; 404; 409 until the status is `Uploaded` |
| `DELETE /api/teams/{teamId}/emails/{emailId}/attachments/{id}` | `ComposeTeamEmails` and `CanAttachDocuments` | 204 | 403, 404, 409 |

The team check from [Endpoints](#endpoints) runs first. `POST` and `DELETE` then fail with 403 when the caller lacks `CanAttachDocuments` in the team (see [Users and permissions](users-and-permissions.md#roles-and-permissions)); both team roles hold it. They return 409 when the email is no longer a draft. A 404 means the team, the email, or the attachment is not the caller's to see or does not exist. Any member of the team can upload to, download from, and remove attachments from any of its drafts.

### The attachment object

```json
{
  "id": "0199f7a4-8b20-7d11-8c0e-6a5b4c3d2e1f",
  "fileName": "report.pdf",
  "documentType": "Pdf",
  "contentType": "application/pdf",
  "sizeBytes": 482113,
  "status": "Pending",
  "failureReason": null,
  "dateCreated": "2026-11-14T18:05:40+00:00"
}
```

### Upload flow

1. **Send the file** as `multipart/form-data` in a field named `file`.

   ```http
   POST /api/teams/0199fa10-3c4d-7e21-b6a0-9d8c7b6a5f40/emails/0199f7a2-5d1e-7c3b-9a44-1f2e3d4c5b6a/attachments
   Content-Type: multipart/form-data; boundary=----x

   ------x
   Content-Disposition: form-data; name="file"; filename="report.pdf"
   Content-Type: application/pdf

   <bytes>
   ------x--
   ```

2. **Read the 202.** The body is the attachment in status `Pending`, and `Location` points at it. The file is on the server's disk but not yet in storage.
3. **Poll** `GET` on the `Location` until `status` is `Uploaded` or `Failed`. The status moves `Pending`, `Uploading`, then `Uploaded`. `Failed` carries a `failureReason` to show the user, who can remove the attachment and upload it again.
4. **Download** from `.../content` once the status is `Uploaded`. The file is served with `Content-Disposition: attachment` and the stored media type, so a browser saves it instead of rendering it in the app's origin.

Antiforgery is disabled on the upload because the API accepts only bearer tokens, never cookies.

### Validation

A failed check returns a 400 validation problem with the errors under the `file` key. The server checks the file twice, once on what the request declares and again on what actually reached the disk.

| Check | Rule |
| --- | --- |
| Name | Present, at most 255 characters. Any folder path a browser sent is removed. |
| Type | The extension must be `.pdf`, `.docx`, `.pptx`, `.png`, `.jpg`, `.jpeg`, `.gif`, or `.webp`. |
| Content | The file's leading bytes must match its extension, so a renamed file of another kind is refused. |
| Size, per file | Not empty, at most 25 MB. |
| Size, per email | The attachments together are at most 25 MB. `Failed` attachments hold no storage and do not count. |

The content check reads the first 12 bytes against each type's signature: `%PDF-` for PDF; `PK` and the ZIP header for `.docx` and `.pptx`; the PNG, JPEG, and GIF signatures; and `RIFF....WEBP` for WebP. The Office formats are ZIP packages, so the signature cannot tell a `.docx` from a `.pptx`; the extension does. This is a guard against honest mistakes and renamed files, not against a crafted file. See [Known limitations](#known-limitations).

Parallel uploads to one draft cannot each pass the 25 MB cap: the size sum and the insert run under a lock per email. Kestrel's default request limit of 30,000,000 bytes sits just above the file cap, so no endpoint-specific limit is set.

### The background pipeline

```text
request --> local disk --> row saved (Pending) --> bounded channel --> AttachmentUploadProcessor --> Blob Storage
              staging                                (QueueCapacity)    (MaxConcurrency workers)
```

1. `AttachmentService.StageAsync` writes the file to the staging directory, named by the attachment id and never by the client's file name, then saves the row as `Pending` and writes the id to the channel.
2. The channel (`AttachmentUploadChannel`) holds up to `QueueCapacity` ids. When it is full, new uploads wait rather than grow memory without limit.
3. `AttachmentUploadProcessor`, a hosted service, runs `MaxConcurrency` workers. Each marks the row `Uploading`, streams the staged file to the blob named `BlobName`, marks it `Uploaded`, and deletes the staged file. A second queued copy of an id a worker already holds is skipped, so two workers never race on one attachment.
4. A storage or database fault is retried. The row stays `Pending` or `Uploading` and the staged file stays on disk. The wait before each retry starts at 2 seconds and doubles up to 1 minute. After `MaxUploadAttempts` tries (6 by default, about a minute of outage) the worker gives up: it deletes the blob the failed attempts may have written (best effort), marks the row `Failed` with the message "The document could not be stored. Remove it and upload it again.", removes the staged file, and logs the cause. The row is marked `Failed` only while it is still `Pending` or `Uploading`, so a failure never overwrites an upload that already finished.
5. A staged file that is missing fails the upload at once, because the only copy of the document is gone and a retry cannot help.

One failed upload never stops the workers. Staging on local disk first means the request can finish as soon as the file is safe, and a short storage outage does not lose the user's upload: the file waits on disk while the retries run. An outage longer than the retries cover ends in `Failed`, and the user uploads the document again. The attempt count is kept in memory. A restart or shutdown leaves the row and the staged file, and [startup recovery](#startup-recovery) queues the upload again with a fresh count.

### Removing an attachment during an upload

A user can remove an attachment at any point. The removal wins:

The removal runs in three steps:

1. It deletes the blob first. If storage fails here, the client gets an error and the attachment is still there, so the client can retry the removal.
2. It soft-deletes the row with a set-based update that bumps the row's version.
3. It deletes the blob once more, best effort, for an upload that finished between steps 1 and 2. A failure here is logged and does not fail the request, because the removal has already succeeded.

What the worker does depends on when the removal lands:

- **Before the worker starts:** the worker finds no active row, deletes the staged file, and skips it.
- **While it uploads:** when the worker saves `Uploaded`, the stale version makes the save fail, the worker sees the row is gone, and it deletes the blob it just wrote.
- **After it uploaded:** step 1 deletes the blob.

If the row changed under the worker and still exists, the worker leaves a row that is `Uploaded` or `Failed` as it is, and retries the upload of one that is still pending.

### Startup recovery

If the process stops with uploads queued, the channel is lost but the rows and staged files are not. When the processor starts, and `RecoverOnStartup` is true, it settles what the last run left behind:

- A `Pending` or `Uploading` row created before this start whose staged file still exists is queued again.
- One whose staged file is gone is marked `Failed` with "The upload was interrupted. Remove the document and upload it again."
- A staged file written before this start that matches no pending row is deleted: a removed attachment, or a request that crashed before its row was saved.

Rows and files from after this start are left alone, because the requests that created them are queuing them already. The cutoff time is taken in the processor's `StartAsync`, which runs during host startup before the server takes requests, so recovery never re-queues or sweeps an upload that a request in the same run queued. Recovery is best effort: if it fails, it logs the error and the processor still takes new uploads.

### Scaling out

The pipeline assumes **one API instance**. Three things tie it there:

- The staging directory is local, so a second instance cannot read the files the first one staged.
- The channel is in memory, so a second instance does not see the first one's queue.
- Recovery treats every `Pending` or `Uploading` row without a staged file on its own disk as interrupted, and fails it. With two instances, a starting instance fails the other's uploads in flight.

Until the pipeline changes, run one instance. Before you scale out:

1. Replace the local staging directory and the in-memory channel with something every instance shares, such as a storage queue and a shared staging container, or upload straight to blob storage from the request.
2. Make recovery claim work per instance instead of failing rows it did not stage. Until then, set `AttachmentUpload:RecoverOnStartup` to `false` on every instance except one.

## Configuration

### Blob storage

| Key | Notes |
| --- | --- |
| `BlobStorage:ConnectionString` | Required; the API refuses to start without it. Set it in user secrets, never in committed config. Use `UseDevelopmentStorage=true` locally with Azurite. |
| `BlobStorage:AttachmentsContainer` | The container for documents. Default `attachments`. 3 to 63 lowercase letters, digits, or single hyphens, starting and ending with a letter or digit; a bad name fails at startup instead of on the first upload. |

`BlobStorageBootstrapper`, a hosted service, creates the container in the background and retries with a growing delay (1 second up to 1 minute, 10 seconds per attempt) until storage answers. Like an unreachable database, unreachable storage does not stop or hold the host. Until the container exists, `/health/ready` reports the problem. An upload accepted in that window is not lost: the worker's [retries](#the-background-pipeline) cover it, so it is stored once the container appears, as long as that happens within the retry window (about a minute with the default `MaxUploadAttempts`). A longer outage ends in `Failed`.

`/health/ready` now has two checks, `sql` and `blob`. The `blob` check passes only when the attachments container exists, and gives up after 5 seconds. `/health/live` is unchanged.

### Upload settings

The `AttachmentUpload` section is optional; each key has a default. The values are validated at startup.

| Key | Default | Notes |
| --- | --- | --- |
| `StagingPath` | empty | An absolute path, or empty for `andes-mailer/attachments` under the system temporary directory. |
| `MaxFileBytes` | 26214400 (25 MB) | The largest file. Must be above 0. |
| `MaxEmailBytes` | 26214400 (25 MB) | The largest total per email. Must be at least `MaxFileBytes`. |
| `MaxConcurrency` | 4 | Uploads at once, 1 to 64. |
| `QueueCapacity` | 256 | Staged files that can wait for a worker, 1 to 10,000. |
| `MaxUploadAttempts` | 6 | Tries per upload before it is marked `Failed`, 1 to 20. Retries wait 2 seconds, then twice as long each time up to 1 minute, so the default covers about a minute of storage being unreachable. |
| `RecoverOnStartup` | `true` | Whether the processor settles interrupted uploads at start. See [Scaling out](#scaling-out). |

## Run it locally

Uploads need a blob store. [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) is the Azure Storage emulator.

1. Start Azurite's blob service. `--skipApiVersionCheck` is needed because the Blob SDK sends a newer service version than the emulator's release knows.

   ```bash
   docker run -p 10000:10000 mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0 --skipApiVersionCheck
   ```

2. From `andes-mailer-api/`, point the API at it, once per machine:

   ```bash
   dotnet user-secrets set "BlobStorage:ConnectionString" "UseDevelopmentStorage=true" --project Andes.Mailer.Api
   ```

3. Apply the migration, then run the API as described in `AGENTS.md`:

   ```bash
   dotnet ef database update --project Andes.Mailer.Repository --startup-project Andes.Mailer.Api
   dotnet run --project Andes.Mailer.Api
   ```

   `/health/ready` reports `blob` as healthy once the container exists.

Calling the email routes needs a [team](teams.md): create one as the seeded administrator, then add members. The seeded administrator has every permission in every team; a user you add with no team has none. After you apply `AddTeams` to a database that already holds drafts, they are gone and their blobs are orphaned in the `attachments` container; see [Upgrading](teams.md#upgrading-the-addteams-migration).

`dotnet test` needs no setup beyond Docker. The integration suite starts its own Azurite container (`mcr.microsoft.com/azure-storage/azurite:3.37.0`, pinned so an emulator release cannot change a run, with `--skipApiVersionCheck`) once for the whole run, next to the SQL Server container.

## Known limitations

- **No sending.** Nothing moves a draft to `Sent`; the status is there for the sending feature. The team's service account will be the sender.
- **No per-author protection.** Any team member can edit or delete a draft another member started. There is no lock, so two members editing at once is caught only by the `version` check, as a 409.
- **Deleting an email leaves its documents in storage.** `DELETE /api/teams/{teamId}/emails/{id}` soft-deletes the email, and its recipient and attachment rows with it as far as queries go, but it does not delete the blobs. Remove attachments first if the documents must go.
- **A single instance only.** See [Scaling out](#scaling-out).
- **No malware scanning.** The content check confirms a file's type, not that it is safe. Treat a download as untrusted.
- **Recipient names do not refresh.** A recipient's `userId` and `displayName` are set when the address is added.
- **No UI.** The UI has no screens for emails or attachments yet.
