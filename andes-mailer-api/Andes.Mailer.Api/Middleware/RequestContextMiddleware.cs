using Andes.Mailer.Common.Constants;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.Identity.Web;

namespace Andes.Mailer.Api.Middleware;

/// <summary>
/// Puts the caller and the routed team on the logs the request writes after authentication and on the request's trace.
/// </summary>
/// <remarks>
/// It runs right after authentication, the first point the caller is known. The caller is the Entra object id only, never a name or an
/// email address; <see cref="TelemetryNames.EndUserIdTag"/> becomes <c>user_AuthenticatedId</c> on the request in Application Insights.
/// The exception handler runs outside it, so its log names the caller itself.
/// </remarks>
internal sealed class RequestContextMiddleware(RequestDelegate next, ILogger<RequestContextMiddleware> logger)
{
    /// <summary>
    /// The route parameter that carries the team id on every route under <c>/api/teams/{teamId}</c>.
    /// </summary>
    public const string TeamIdRouteValue = "teamId";

    private readonly RequestDelegate _next = next;
    private readonly ILogger<RequestContextMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.GetObjectId();
        var teamId = context.GetRouteValue(TeamIdRouteValue) as string;
        if (userId is null && teamId is null)
        {
            await _next(context);
            return;
        }

        var activity = context.Features.Get<IHttpActivityFeature>()?.Activity;
        List<KeyValuePair<string, object?>> scope = [];
        if (userId is not null)
        {
            activity?.SetTag(TelemetryNames.EndUserIdTag, userId);
            scope.Add(new(TelemetryNames.UserIdProperty, userId));
        }

        if (teamId is not null)
        {
            activity?.SetTag(TelemetryNames.TeamIdTag, teamId);
            scope.Add(new(TelemetryNames.TeamIdProperty, teamId));
        }

        using (_logger.BeginScope(scope))
        {
            await _next(context);
        }
    }
}
