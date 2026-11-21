# API telemetry

The API answers a request, calls Microsoft Graph, SQL Server, and Blob Storage, and writes log lines along the way. This page explains how those pieces end up under one trace id in Application Insights, what the API adds on top of what the distro collects on its own, and how to configure and test it. For the setup that both apps share, see the [overview](README.md). For lookups, see [Queries](queries.md).

## How it fits together

- **The Microsoft OpenTelemetry Distro** (`Microsoft.OpenTelemetry` 1.2.0) collects ASP.NET Core requests, `HttpClient` calls (Graph), SqlClient calls, Azure SDK calls (Blob), and `ILogger` logs, and exports them to Azure Monitor. `AddAndesMailerTelemetry` in `Andes.Mailer.Api/Observability/TelemetryRegistration.cs` registers it.
- **Two small middleware classes** add what the distro cannot know: the caller and the team, and one log line per request.
- **Background work** starts activities of its own from the `Andes.Mailer` source, so a dispatch run or an upload has a trace.
- **`TelemetryNames`** (`Andes.Mailer.Common/Constants/TelemetryNames.cs`) holds every name, tag, and log property, so the code and [Queries](queries.md) agree.

The pipeline order in `Program.cs` matters:

| Order | Step | Why it sits there |
| --- | --- | --- |
| 1 | `UseAndesMailerRequestLogging` | Runs ahead of `UseExceptionHandler`, so it logs the final status, including statuses the exception handler maps from exceptions |
| 2 | `UseExceptionHandler`, `UseStatusCodePages`, `UseHttpsRedirection` | Unchanged |
| 3 | `UseAuthentication` | The caller becomes known |
| 4 | `UseAndesMailerRequestContext` | The first point the caller is known, so everything after it carries the `oid` |
| 5 | `UseAuthorization`, and the rest | Unchanged |

## Configuration

The `ApplicationInsights` section is optional (`ApplicationInsightsOptions`, in `Andes.Mailer.Api/Options/`). The API validates it when it starts.

| Key | Default | Notes |
| --- | --- | --- |
| `ConnectionString` | empty | The resource's connection string. It must hold an `InstrumentationKey=` part, or the API refuses to start. When empty, the API reads the `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable, which App Service sets. |
| `TracesPerSecond` | 5 | The most traces a second the API sends, from 0.01 to 1000. A trace beyond it is dropped whole. Logs are never dropped. |

Set the connection string in user secrets locally, from `andes-mailer-api/`:

```bash
dotnet user-secrets set "ApplicationInsights:ConnectionString" "InstrumentationKey=...;IngestionEndpoint=https://...;LiveEndpoint=https://..." --project Andes.Mailer.Api
```

`appsettings.json` holds an empty string and `appsettings.sample.json` shows the shape. Never commit a real value to `appsettings.json`.

### Without a connection string

The API still traces. It creates the same activities and puts the trace id in its log line and in Problem Details, but it exports nothing (`ExportTarget.None`). The integration tests rely on this, so they never send data to a real resource.

The exporter is set explicitly, so a stray OTLP or Agent 365 setting in the environment never turns another exporter on.

### What the registration sets

| Setting | Value | Why |
| --- | --- | --- |
| `service.name` | `andes-mailer-api` | Becomes `cloud_RoleName` |
| `EnableTraceBasedLogsSampler` | `false` | The distro drops logs of unsampled traces by default. Off, every request keeps its log line when its trace is sampled out. |
| `IncludeScopes` | `true` | Log scope properties, such as `UserId` and `TeamId`, land in `customDimensions` |
| Extra activity source | `Andes.Mailer` | The background activities below |
| ASP.NET Core trace filter | Skips `/health/*` | Probes poll every few seconds, so tracing them would bury real requests and spend the sampling budget |
| Live Metrics | On | The distro's default |

## What each request records

### The request trace

For an authenticated request, `RequestContextMiddleware` tags the request's activity:

| Tag | Value | In Application Insights |
| --- | --- | --- |
| `enduser.id` | The caller's `oid` | `user_AuthenticatedId` on `requests` |
| `andes.team_id` | The `{teamId}` route value, on routes under `/api/teams/{teamId}` | `customDimensions["andes.team_id"]` on `requests` |

It also opens a log scope with `UserId` and `TeamId`, so every log written later in the request has them in `customDimensions`. The middleware does nothing when the request has neither a caller nor a team.

Only the `oid` is used, never a name or an email address.

`GlobalExceptionHandler` runs outside that scope, so its error log names the caller itself: "Unhandled exception for {Method} {Path} for user {UserId}".

### The request log line

`RequestLoggingMiddleware` writes one line per request, when the request ends:

```text
HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms for user {UserId} (route {RouteTemplate}, team {TeamId}, trace {TraceId}, client aborted {ClientAborted})
```

| Property | Holds |
| --- | --- |
| `Method`, `Path` | The HTTP method and the path, without the query string |
| `StatusCode` | The status the client gets, including statuses mapped from exceptions |
| `ElapsedMs` | Time spent in the pipeline, in milliseconds |
| `UserId` | The caller's `oid`, empty for an anonymous request |
| `RouteTemplate` | The matched route, such as `/api/teams/{teamId:guid}/emails`, or empty when no route matched |
| `TeamId` | The `{teamId}` route value, if any |
| `TraceId` | The 32-hex trace id, equal to `operation_Id` |
| `ClientAborted` | Whether the client closed the connection before the response |

The level is Information, or Warning for a 5xx. An exception that gets past the exception handler before the response starts is logged as 500.

The line never holds query strings, headers, or bodies. Health probes (`/health/*`) are skipped.

The route, the team, and the start time are read before the call, because the exception handler clears the endpoint and route values before it writes its response.

### Error responses

A Problem Details response carries `traceId` in W3C format, `00-<trace id>-<span id>-01`. The second segment is the `operation_Id`, so a user who reports an error can hand you the id. `split(traceId, '-')[1]` extracts it in KQL.

## Background work

Background services start activities from the `Andes.Mailer` source (`AndesMailerActivitySource`). Application Insights shows them in `dependencies`, because they are internal operations, with the tags in `customDimensions`.

| Activity | When | Tags | Shape |
| --- | --- | --- | --- |
| `email.dispatch` | One per run of `EmailDispatchProcessor` | | Root activity. The run's SQL and Graph calls, its deliveries, and its logs share its trace. |
| `email.deliver` | One per delivery attempt | `andes.delivery_id` | Child of `email.dispatch` |
| `attachment.upload` | One per upload attempt in `AttachmentUploadProcessor` | `andes.attachment_id`, `andes.attachment_owner` (`Email` or `Template`) | Root activity: the request that staged the file already answered 202, so the upload is an operation of its own |

All three get Error status when they fault, which shows as `success == false`.

- `email.dispatch` starts on every run, including runs with nothing due. With the default `EmailSending:Schedule` of `* * * * *`, that is about one a minute, so expect rows with no children. Filter on `email.deliver` to see real work.
- The request that sends or schedules an email is not part of the dispatch trace. Follow an email through its delivery ids instead; see [Queries](queries.md#email-dispatch).
- These activities are traced, so the rate limit applies to them. Their logs are not sampled.

### Logs that carry ids

The processors' `[LoggerMessage]` logs put their ids in `customDimensions`, with no request scope. The ones to know:

| Id property | Logs | Example message |
| --- | --- | --- |
| `DeliveryId` | `EmailDispatchProcessor` | "Sent delivery {DeliveryId} on attempt {Attempt}", "Delivery {DeliveryId} failed on attempt {Attempt}; retrying in {Delay}", "Delivery {DeliveryId} failed on its last attempt ({Attempt})", "The mail server rejected delivery {DeliveryId}; it is not tried again", "The mailbox of delivery {DeliveryId} is throttled; trying again in {Delay}", "Delivery {DeliveryId} faulted; its claim runs out and a later run retries it" |
| `EmailId`, `Status`, `Sent`, `Total` | `EmailDispatchProcessor` | "Email {EmailId} finished as {Status}: {Sent} of {Total} recipients sent" |
| `AttachmentId` | `AttachmentUploadProcessor` | "Stored attachment {AttachmentId}", "Could not store attachment {AttachmentId} (attempt {Attempt}); retrying in {Delay}", "Could not store attachment {AttachmentId}" |

A log written with an exception, such as "Delivery {DeliveryId} failed on its last attempt", reaches Application Insights as a row in `exceptions`, not `traces`. [Queries](queries.md#email-dispatch) reads both.

## Add telemetry to new code

- **A new background operation.** Start an activity from `AndesMailerActivitySource.Instance`, name it with a constant in `TelemetryNames`, set `ActivityStatusCode.Error` on a fault, and add its tags to `TelemetryNames` too. Use `using var activity = ...` and null-conditional calls, because `StartActivity` returns `null` when nothing listens.
- **A new log.** Use `[LoggerMessage]` and put ids in the message template, so they land in `customDimensions`. Never log an email address, a name, a subject, a body, or a query string.
- **A new route under a team.** Nothing to do. The `{teamId}` route value is picked up by name (`teamId`).

## Known limitations

- **Sampled traces, complete logs.** A trace can be missing; its log lines are not. See the [overview](README.md#sampling).
- **Anonymous requests have no user.** A request that never authenticates has no `UserId`, `enduser.id`, or `user_AuthenticatedId`.
- **Background logs have no caller.** A dispatch or upload log has ids such as `DeliveryId`, but no `UserId`, because no request scope is open.
- **One resource per environment, not per app.** The API and the UI share a resource, so roles filter, not resources.
- **The test host reads the environment.** If `APPLICATIONINSIGHTS_CONNECTION_STRING` is set on your machine, the integration tests export to that resource. Unset it before you run `dotnet test`.

## Testing

`dotnet test` from `andes-mailer-api/` covers the telemetry without a resource:

| Test | What it checks |
| --- | --- |
| `Unit.Test/Api/Middleware/RequestLoggingMiddlewareTests.cs` | The line's caller, route, status, and duration; the trace id; an anonymous request; skipped health probes; a fault that reaches the middleware, logged as a warning and rethrown; and a route read before the exception handler clears it |
| `Unit.Test/Api/Middleware/RequestContextMiddlewareTests.cs` | The `UserId` and `TeamId` log scope, the activity tags (using `FakeHttpActivityFeature` to give the request an activity), and no scope or tag for an anonymous caller |
| `Unit.Test/Api/Options/ApplicationInsightsOptionsValidatorTests.cs` | The connection string and `TracesPerSecond` rules |
| `Unit.Test/Service/Emails/Attachments/AttachmentUploadProcessorTests.cs`, `.../Sending/EmailDispatchProcessorTests.cs` | The upload as a root activity that ends in Error when the store fails, and a dispatch run with each delivery under it, using `ActivityCollector` to listen to the `Andes.Mailer` source |
| `Integration.Test/Middleware/RequestLoggingIntegrationTests.cs` | The line, end to end, with `FakeLogger`: the caller, route, status, and a 32-hex trace id; the 404 the exception handler writes for another team's route; and no line for a health probe |
| `Integration.Test/Startup/TelemetryStartupIntegrationTests.cs` | Probes are not traced; other requests are |

The integration suite needs Docker, as the rest of it does.
