using Andes.Mailer.Api.Health;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.Identity.Web;

namespace Andes.Mailer.Api.Middleware;

/// <summary>
/// Writes one log line per request with its trace id, caller, route, status, and duration.
/// </summary>
/// <remarks>
/// It runs first in the pipeline, ahead of the exception handler, so it logs the status the client really gets, including the ones the
/// exception handler maps from exceptions. It never logs the query string, headers, or bodies, because recipients and email contents are
/// personal data. Health probes are skipped.
/// </remarks>
internal sealed partial class RequestLoggingMiddleware(RequestDelegate next, TimeProvider time, ILogger<RequestLoggingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly TimeProvider _time = time;
    private readonly ILogger<RequestLoggingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments(HealthRegistration.BasePath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Read before the call: the exception handler clears the endpoint and route values before it writes its response.
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        var teamId = context.GetRouteValue(RequestContextMiddleware.TeamIdRouteValue) as string;
        var started = _time.GetTimestamp();
        var faulted = false;
        try
        {
            await _next(context);
        }
        catch
        {
            faulted = true;
            throw;
        }
        finally
        {
            LogCompleted(context, route, teamId, _time.GetElapsedTime(started), faulted);
        }
    }

    #region Private methods

    private void LogCompleted(HttpContext context, string? route, string? teamId, TimeSpan elapsed, bool faulted)
    {
        // An exception that got past the exception handler before the response started reaches the client as a 500.
        var status = faulted && !context.Response.HasStarted ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
        var level = status >= StatusCodes.Status500InternalServerError ? LogLevel.Warning : LogLevel.Information;
        if (!_logger.IsEnabled(level))
        {
            return;
        }

        var userId = context.User.GetObjectId();
        var traceId = context.Features.Get<IHttpActivityFeature>()?.Activity.TraceId.ToHexString() ?? context.TraceIdentifier;

        LogRequestCompleted(
            _logger,
            level,
            context.Request.Method,
            context.Request.Path,
            status,
            elapsed.TotalMilliseconds,
            userId,
            route,
            teamId,
            traceId,
            context.RequestAborted.IsCancellationRequested);
    }

    #endregion

    #region Logging

    [LoggerMessage(Message = "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0.0} ms for user {UserId} (route {RouteTemplate}, team {TeamId}, trace {TraceId}, client aborted {ClientAborted})")]
    private static partial void LogRequestCompleted(
        ILogger logger,
        LogLevel level,
        string method,
        PathString path,
        int statusCode,
        double elapsedMs,
        string? userId,
        string? routeTemplate,
        string? teamId,
        string traceId,
        bool clientAborted);

    #endregion
}
