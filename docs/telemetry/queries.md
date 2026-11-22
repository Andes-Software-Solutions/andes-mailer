# Telemetry queries

Telemetry only helps if you can ask it the question in front of you: what happened to this request, what did this person do, which calls are slow. These Kusto Query Language (KQL) queries answer the questions you reach for most. Run them in the Application Insights resource under **Logs**. For how the data gets there, see the [overview](README.md), the [API](api.md), and the [UI](ui.md).

## Before you run them

- **Table names.** The queries use the Application Insights names (`requests`, `dependencies`, `traces`, `exceptions`, `pageViews`, `customEvents`). In a Log Analytics workspace they are `AppRequests`, `AppDependencies`, `AppTraces`, `AppExceptions`, `AppPageViews`, and `AppEvents`, with different column names. Run these from the Application Insights resource.
- **Ingestion delay.** New items take 2 to 5 minutes to appear.
- **Sampling.** The API sends at most 5 traces a second, so a `requests` or `dependencies` row can be missing, and so can the background activities. Logs are not sampled, so the [request log line](#the-request-log-line) is the complete record of every request. When you count sampled tables, use `sum(itemCount)` instead of `count()`. The UI is not sampled.
- **Role names.** `cloud_RoleName` is `andes-mailer-api` or `andes-mailer-ui`.
- **Time range.** Every query has a `timestamp > ago(...)` filter. Narrow it; it makes the query faster and cheaper.
- **Not run against a live resource.** These queries use the column names from the Application Insights data model and the property names in the code. If a column comes back empty, check the property name in [API](api.md) first.
- **Booleans.** `success` is filtered as `tostring(success) =~ "false"`, which works whether your resource returns it as a boolean or as text.

## Everything for one operation

An operation is a trace: a page view and the API calls it made, or one request and everything it touched. Use it when someone gives you an error, a trace id, or a time.

```kusto
let operationId = "4bf92f3577b34da6a3ce929d0e0e4736";
union requests, dependencies, traces, exceptions, pageViews, customEvents
| where timestamp > ago(7d)
| where operation_Id == operationId
| project timestamp, itemType, cloud_RoleName,
          what = coalesce(name, message, outerMessage),
          resultCode, duration, severityLevel, operation_ParentId
| order by timestamp asc
```

The result reads top to bottom: the page view, the API request, the SQL and Graph calls under it, and the log lines.

### Where the operation id comes from

| You have | Get `operation_Id` |
| --- | --- |
| A Problem Details `traceId`, such as `00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01` | The second segment |
| The request log line | Its `TraceId` property, already the same value |

In KQL, the second segment of a `traceId`:

```kusto
print operationId = tostring(split("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", "-")[1])
```

Expected output: one row, `operationId` = `4bf92f3577b34da6a3ce929d0e0e4736`.

A trace that sampling dropped has no `requests` or `dependencies` rows, but its log lines are still there, with the same `operation_Id`.

## One user's activity

People are identified by Entra object id (`oid`), the same value as `[Core].[User].Id`. UI items carry it in `user_AuthenticatedId`. The API's request lines carry it in `customDimensions.UserId`.

A timeline across UI and API:

```kusto
let user = "00000000-0000-0000-0000-000000000000";
let ui = union pageViews, customEvents, exceptions
    | where timestamp > ago(1d)
    | where user_AuthenticatedId == user
    | project timestamp, itemType, cloud_RoleName,
              what = coalesce(name, outerMessage),
              detail = tostring(customDimensions.route), operation_Id;
let api = traces
    | where timestamp > ago(1d)
    | where message startswith "HTTP " and tostring(customDimensions.UserId) == user
    | project timestamp, itemType, cloud_RoleName,
              what = strcat(tostring(customDimensions.Method), " ", tostring(customDimensions.Path)),
              detail = tostring(customDimensions.StatusCode), operation_Id;
union ui, api
| order by timestamp asc
```

`detail` is the route for a page view and the status for an API request. The API side comes from the request log line, because it covers every request, and `requests` rows can be sampled out.

Every log the API wrote for that user, including error logs (which land in `exceptions`):

```kusto
let user = "00000000-0000-0000-0000-000000000000";
union traces, exceptions
| where timestamp > ago(1d)
| where tostring(customDimensions.UserId) == user
| project timestamp, itemType, severityLevel, what = coalesce(message, outerMessage), operation_Id
| order by timestamp asc
```

Items the UI sent before it knew the account have no `user_AuthenticatedId`; see [UI known limitations](ui.md#known-limitations).

## The request log line

One line per API request, with the status the client got. Health probes are not logged. Its message starts with "HTTP ".

```kusto
traces
| where timestamp > ago(1h)
| where cloud_RoleName == "andes-mailer-api" and message startswith "HTTP "
| project timestamp, severityLevel,
          method = tostring(customDimensions.Method),
          path = tostring(customDimensions.Path),
          status = toint(customDimensions.StatusCode),
          elapsedMs = todouble(customDimensions.ElapsedMs),
          userId = tostring(customDimensions.UserId),
          route = tostring(customDimensions.RouteTemplate),
          teamId = tostring(customDimensions.TeamId),
          traceId = tostring(customDimensions.TraceId),
          clientAborted = tolower(tostring(customDimensions.ClientAborted)) == "true",
          operation_Id
| order by timestamp desc
```

`traceId` equals `operation_Id`. `severityLevel` is 1 (Information) or 2 (Warning, for a 5xx). `route` is empty when no route matched.

## Failed API requests

By route, status, and user, from the request log line, so no request is missing:

```kusto
traces
| where timestamp > ago(1d)
| where cloud_RoleName == "andes-mailer-api" and message startswith "HTTP "
| extend status = toint(customDimensions.StatusCode),
         method = tostring(customDimensions.Method),
         route = tostring(customDimensions.RouteTemplate),
         userId = tostring(customDimensions.UserId)
| where status >= 400
| summarize failures = count(), lastSeen = max(timestamp) by method, route, status, userId
| order by failures desc
```

Change the filter to `status >= 500` to see server faults only. A 404 on a `/api/teams/{teamId}` route is also what the API answers to someone outside the team, so it is not always a bug.

The same from the traced requests, which also carry the caller as `user_AuthenticatedId`:

```kusto
requests
| where timestamp > ago(1d)
| where cloud_RoleName == "andes-mailer-api" and toint(resultCode) >= 400
| summarize failures = sum(itemCount) by name, resultCode, user_AuthenticatedId
| order by failures desc
```

## API latency per route

Percentiles from the request log line, which is not sampled. Requests the client abandoned are left out, because their time says nothing about the API.

```kusto
traces
| where timestamp > ago(1d)
| where cloud_RoleName == "andes-mailer-api" and message startswith "HTTP "
| where tolower(tostring(customDimensions.ClientAborted)) != "true"
| extend route = strcat(tostring(customDimensions.Method), " ", tostring(customDimensions.RouteTemplate)),
         elapsedMs = todouble(customDimensions.ElapsedMs)
| summarize requests = count(),
            p50 = percentile(elapsedMs, 50),
            p95 = percentile(elapsedMs, 95),
            p99 = percentile(elapsedMs, 99)
    by route
| order by p95 desc
```

The same from `requests`, whose `duration` is in milliseconds. This one is sampled, but it links to the [operation](#everything-for-one-operation) and its dependencies:

```kusto
requests
| where timestamp > ago(1d) and cloud_RoleName == "andes-mailer-api"
| summarize requests = sum(itemCount),
            p50 = percentile(duration, 50),
            p95 = percentile(duration, 95),
            p99 = percentile(duration, 99)
    by name
| order by p95 desc
```

## Usage

### Page views

Views and distinct people per screen. `route` groups every team's page as one.

```kusto
pageViews
| where timestamp > ago(30d) and cloud_RoleName == "andes-mailer-ui"
| summarize views = count(), users = dcount(user_AuthenticatedId)
    by route = tostring(customDimensions.route), name
| order by views desc
```

### Daily and monthly active users

An active user is a distinct `oid` with at least one page view.

```kusto
pageViews
| where timestamp > ago(30d) and cloud_RoleName == "andes-mailer-ui"
| where isnotempty(user_AuthenticatedId)
| summarize dau = dcount(user_AuthenticatedId) by day = bin(timestamp, 1d)
| order by day asc
```

```kusto
pageViews
| where timestamp > ago(180d) and cloud_RoleName == "andes-mailer-ui"
| where isnotempty(user_AuthenticatedId)
| summarize mau = dcount(user_AuthenticatedId) by month = startofmonth(timestamp)
| order by month asc
```

How far back you can look depends on the retention of the Log Analytics workspace behind the resource.

### Sign-ins per day

The UI records the custom event `UserSignedIn` on an interactive sign-in. A session restored from the browser's cache records none, so this counts sign-ins, not active users.

```kusto
customEvents
| where timestamp > ago(30d) and name == "UserSignedIn"
| summarize signIns = count(), users = dcount(user_AuthenticatedId) by day = bin(timestamp, 1d)
| order by day asc
```

## UI to API calls

Every call the browser makes to the API, grouped by operation (ids replaced by `{id}`) and the screen it was made from, with failures and slow calls. The `has "/api/"` filter leaves out other calls the browser makes, such as MSAL's token requests.

```kusto
dependencies
| where timestamp > ago(1d) and cloud_RoleName == "andes-mailer-ui"
| extend apiOperation = tostring(customDimensions.apiOperation),
         page = tostring(customDimensions.page)
| where apiOperation has "/api/"
| summarize calls = count(),
            failures = countif(tostring(success) =~ "false"),
            p95Ms = percentile(duration, 95),
            slow = countif(duration > 2000)
    by apiOperation, page
| order by failures desc, calls desc
```

`slow` counts calls over 2 seconds; change the threshold to suit. To see the failed calls themselves, with the id to jump to the [whole operation](#everything-for-one-operation):

```kusto
dependencies
| where timestamp > ago(1d) and cloud_RoleName == "andes-mailer-ui"
| where tostring(success) =~ "false"
| extend apiOperation = tostring(customDimensions.apiOperation), page = tostring(customDimensions.page)
| where apiOperation has "/api/"
| project timestamp, apiOperation, page, resultCode, duration, user_AuthenticatedId, operation_Id
| order by timestamp desc
```

## Errors

### Browser exceptions

Grouped by type and message, with how many people hit each. An `HttpErrorResponse` is reported as `HTTP <status> <operation>`.

```kusto
exceptions
| where timestamp > ago(1d)
| where client_Type == "Browser" or cloud_RoleName == "andes-mailer-ui"
| summarize occurrences = count(), users = dcount(user_AuthenticatedId), lastSeen = max(timestamp)
    by type, outerMessage
| order by occurrences desc
```

### Server exceptions with the caller

The exception handler's error log carries the caller (`UserId`). A join to the request log line adds the route and the status. A background exception has no request, so its route is empty.

```kusto
let requestLines = traces
    | where timestamp > ago(1d) and message startswith "HTTP "
    | project operation_Id,
              route = tostring(customDimensions.RouteTemplate),
              status = toint(customDimensions.StatusCode);
exceptions
| where timestamp > ago(1d) and cloud_RoleName == "andes-mailer-api"
| extend userId = tostring(customDimensions.UserId)
| join kind=leftouter requestLines on operation_Id
| project timestamp, type, outerMessage, userId, route, status, operation_Id
| order by timestamp desc
```

## Email dispatch

`EmailDispatchProcessor` starts an `email.dispatch` activity per run and an `email.deliver` child per delivery, tagged `andes.delivery_id`. Its logs carry `DeliveryId` and `EmailId`. A run starts about once a minute even with nothing due, so look at `email.deliver` for real work.

Deliveries per hour, and how many faulted (sampled, so a count, not a record):

```kusto
dependencies
| where timestamp > ago(1d) and name == "email.deliver"
| summarize deliveries = sum(itemCount),
            faulted = sumif(itemCount, tostring(success) =~ "false")
    by bin(timestamp, 1h)
| order by timestamp asc
```

How each email finished, from its log (not sampled):

```kusto
traces
| where timestamp > ago(7d)
| where message startswith "Email " and message contains " finished as "
| project timestamp,
          emailId = tostring(customDimensions.EmailId),
          status = tostring(customDimensions.Status),
          sent = toint(customDimensions.Sent),
          total = toint(customDimensions.Total),
          operation_Id
| order by timestamp desc
```

Delivery warnings and errors: retries, throttling, rejections, and final failures. Logs written with an exception are in `exceptions`, so the query reads both. `severityLevel` 2 is Warning, 3 is Error.

```kusto
union traces, exceptions
| where timestamp > ago(7d)
| where isnotempty(tostring(customDimensions.DeliveryId)) and severityLevel >= 2
| project timestamp, severityLevel,
          deliveryId = tostring(customDimensions.DeliveryId),
          attempt = toint(customDimensions.Attempt),
          what = coalesce(message, outerMessage),
          exceptionType = type,
          operation_Id
| order by timestamp desc
```

One delivery's story, across every run that tried it. A delivery that retries appears in several dispatch operations, one per attempt, and each operation is the whole run, so it holds the other deliveries of that run too.

```kusto
let deliveryId = "00000000-0000-0000-0000-000000000000";
let operations = union traces, exceptions, dependencies
    | where timestamp > ago(7d)
    | where tostring(customDimensions.DeliveryId) == deliveryId
         or tostring(customDimensions["andes.delivery_id"]) == deliveryId
    | distinct operation_Id;
union dependencies, traces, exceptions
| where timestamp > ago(7d)
| where operation_Id in (operations)
| project timestamp, itemType, what = coalesce(name, message, outerMessage),
          resultCode, duration, severityLevel, deliveryId = tostring(customDimensions.DeliveryId), operation_Id
| order by operation_Id asc, timestamp asc
```

## Attachment uploads

`AttachmentUploadProcessor` starts an `attachment.upload` activity per attempt, tagged `andes.attachment_id` and `andes.attachment_owner` (`Email` or `Template`). A fault that reaches the retry logic marks the attempt Error, and the processor logs a warning for a retry ("...; retrying in...") and an error when it gives up ("Could not store attachment ...").

Failed attempts:

```kusto
dependencies
| where timestamp > ago(7d)
| where name == "attachment.upload" and tostring(success) =~ "false"
| project timestamp,
          attachmentId = tostring(customDimensions["andes.attachment_id"]),
          owner = tostring(customDimensions["andes.attachment_owner"]),
          duration, operation_Id
| order by timestamp desc
```

The logs behind them, with the exception where there is one (not sampled):

```kusto
union traces, exceptions
| where timestamp > ago(7d)
| where isnotempty(tostring(customDimensions.AttachmentId)) and severityLevel >= 2
| project timestamp, severityLevel,
          attachmentId = tostring(customDimensions.AttachmentId),
          what = coalesce(message, outerMessage),
          exceptionType = type,
          operation_Id
| order by timestamp desc
```

The request that staged the file is a different operation from the upload. Take `attachmentId` from the upload response and search for it here.
