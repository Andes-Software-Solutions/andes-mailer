# Telemetry

When a user says "sending failed at 10:42", someone has to find that one request among thousands, see what it called, and read what the code logged while it ran. Telemetry makes that a lookup instead of a hunt. Both apps report to one Application Insights resource, and every item carries the same trace id, so you can follow a click in the browser through the API, Microsoft Graph, SQL Server, and Blob Storage.

This page covers the model, how to create the resource, and what is and is not collected. For the details of each app, see [API](api.md) and [UI](ui.md). For ready-made lookups, see [Queries](queries.md).

## How it fits together

- **One resource, two roles.** The API and the UI send to the same Application Insights resource. `cloud_RoleName` tells them apart: `andes-mailer-api` and `andes-mailer-ui`.
- **One trace id across both.** The UI starts a new operation for every page view and sends its id to the API in the W3C `traceparent` header. The API's request, its SQL, Graph, and Blob calls, and its log lines then share that id, which Application Insights shows as `operation_Id`. See [Correlation](#correlation).
- **The API traces by default, exports on demand.** With no connection string the API still creates traces, so ids appear in logs and error responses, but it sends nothing. The UI sends nothing until `config.json` has a `telemetry` section.
- **People are identified by Entra object id only.** See [Privacy](#privacy).

| | API | UI |
| --- | --- | --- |
| Role name | `andes-mailer-api` | `andes-mailer-ui` |
| SDK | Microsoft OpenTelemetry Distro, `Microsoft.OpenTelemetry` 1.2.0 | `@microsoft/applicationinsights-web` 3.4.6 |
| Where it is wired | `Andes.Mailer.Api/Observability/TelemetryRegistration.cs` | `src/app/core/telemetry/` |
| Connection string | `ApplicationInsights:ConnectionString`, or `APPLICATIONINSIGHTS_CONNECTION_STRING` | `telemetry.connectionString` in `public/config.json` |
| Off when | No connection string | No `telemetry` section |
| Sampling | Traces, at most 5 a second; logs are never sampled | None |
| Detail | [API](api.md) | [UI](ui.md) |

## Create the Application Insights resource

The repository has no infrastructure as code, so you create the resource by hand, once per environment. Use a separate resource for each environment (local, test, production) so a test run never mixes with real traffic.

Application Insights resources are workspace-based: they store data in a Log Analytics workspace, and that workspace sets retention and billing. Classic resources are retired.

You need permission to create resources in the subscription. If the workspace is in another subscription, you also need `Microsoft.OperationalInsights/workspaces/sharedKeys/action` on it.

### In the Azure portal

1. Create a resource, then search for **Application Insights**.
2. Pick the subscription and resource group, and name the resource, for example `andes-mailer-prod`.
3. Pick the region and a **Log Analytics workspace**. Choose an existing one, or the portal creates one for you.
4. Select **Review + create**, then **Create**.
5. When it finishes, open the resource. On **Overview**, in **Essentials**, copy the **Connection string**.

### With the Azure CLI

```bash
az extension add -n application-insights

az monitor app-insights component create \
  --app andes-mailer-prod \
  --location westeurope \
  --resource-group <resource-group> \
  --kind web \
  --application-type web \
  --workspace "/subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.OperationalInsights/workspaces/<workspace-name>"
```

Read the connection string back with:

```bash
az monitor app-insights component show --app andes-mailer-prod --resource-group <resource-group> --query connectionString --output tsv
```

The connection string looks like `InstrumentationKey=<guid>;IngestionEndpoint=https://<region>.in.applicationinsights.azure.com/;LiveEndpoint=...`. Both apps check that it holds an `InstrumentationKey=` part and refuse a value that does not.

## Connect the apps

| App | Where | Value |
| --- | --- | --- |
| API, locally | User secrets | `dotnet user-secrets set "ApplicationInsights:ConnectionString" "<connection string>" --project Andes.Mailer.Api`, run from `andes-mailer-api/` |
| API, on App Service | Application Insights turned on for the app | App Service sets `APPLICATIONINSIGHTS_CONNECTION_STRING`; the API reads it when `ApplicationInsights:ConnectionString` is empty |
| UI | `andes-mailer-ui/public/config.json` | Add `"telemetry": { "connectionString": "<connection string>" }` |

The UI's connection string is public by nature: the browser downloads it, as it does every Application Insights browser setup. It lets anyone send items to the resource, not read them. Keep it in `config.json` per environment, not in the bundle, so one build can be promoted.

The committed `config.json` has no `telemetry` section, so a UI you run locally sends nothing until you add one. Do not commit the section with a development or production string.

Telemetry from a new resource takes a few minutes to show up. See [Ingestion delay](#ingestion-delay).

## Correlation

Correlation runs over the W3C `traceparent` header (`00-<trace id>-<span id>-<flags>`).

1. On every route change the UI starts a new trace id and records a page view. The API calls that screen makes carry that id in `traceparent`.
2. The API's OpenTelemetry reads the header, so its `requests` row, its dependencies, and its logs use the same trace id. The trace id is `operation_Id` in Application Insights.
3. A page view and the API calls it triggers therefore share one `operation_Id`.

You can get the id from three places when you start from an incident:

| You have | The `operation_Id` is |
| --- | --- |
| A Problem Details response | The second segment of `traceId`: `00-<operation_Id>-<span id>-01`, so `split(traceId, '-')[1]` in KQL |
| The request log line | The `TraceId` property, 32 lowercase hex characters, already equal to `operation_Id` |
| A page view | Its own `operation_Id` |

[Queries](queries.md#everything-for-one-operation) shows how to pull every item of one operation.

Correlation assumes the UI and the API share an origin, as they do with the relative `/api/...` URLs the UI uses. The browser SDK does not add `traceparent` to cross-origin calls unless CORS correlation is turned on, which this setup does not do. See [UI known limitations](ui.md#known-limitations).

## Privacy

- **Users are identified by their Entra object id (`oid`) only**, never a name or an email address. The `oid` is the same value as `[Core].[User].Id`, so you can look the person up in the database when you need to.
- **The API never logs query strings, headers, or request or response bodies.** Recipients and email contents are personal data, and the directory search puts what a user typed in its query string.
- **The UI strips query strings** from page-view URIs, dependency names, and dependency targets, and reports an `HttpErrorResponse` by status and operation, never its URL.
- **Custom events carry no personal data.** Keep it that way when you add one; see [Add a custom event](ui.md#add-a-custom-event).
- **The browser sets cookies** (`ai_user`, `ai_session`, `ai_authUser`) once telemetry is on. Decide whether your deployment needs a consent notice.

## Operating notes

### Ingestion delay

New items take about 2 to 5 minutes to appear in **Logs**. When you test a change, wait before you decide data is missing.

### Sampling

The API sends at most 5 traces a second (`ApplicationInsights:TracesPerSecond`). A trace is kept or dropped whole, so a kept trace is never half there. That affects `requests` and `dependencies`, including the background activities, not logs. The API always sends its logs, so every request keeps its log line even when its trace was dropped.

Two things follow:

- A `requests` row can be missing for a request you know happened. The request log line in `traces` is the complete record; see [The request log line](queries.md#the-request-log-line).
- When you count requests or dependencies, sum `itemCount` rather than `count()`, because sampled items stand for more than one. The UI is not sampled.

### Live Metrics

Live Metrics is on, as the distro's default. Open **Live metrics** on the resource to watch requests, failures, and logs as they happen, with no ingestion delay.

## Known limitations

- **No infrastructure as code.** Create the resource by hand and keep the connection strings in each environment's configuration yourself.
- **Not every request has a trace.** The 5 traces a second limit drops the rest; their log lines remain. See [Sampling](#sampling).
- **No telemetry from a UI without a `telemetry` section.** That includes the committed `config.json`.
- **A bad connection string stops startup.** The API refuses to start when `ApplicationInsights:ConnectionString` has no `InstrumentationKey=` part, and the UI shows "Andes Mailer could not start." when `telemetry.connectionString` has none.
- **Background work is its own operation.** An upload or a send is not part of the request that started it, because that request has already answered. You find it by attachment or delivery id. See [API](api.md#background-work).
- **No alerts or dashboards are defined.** The repository holds queries only.

## Testing

Both apps test their telemetry without a resource: the API with a fake logger and an activity listener, the UI with a fake SDK client. See [API testing](api.md#testing) and [UI testing](ui.md#testing).
