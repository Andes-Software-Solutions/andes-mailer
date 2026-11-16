using Andes.Mailer.Common.Enums;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Security;

namespace Andes.Mailer.Api.Filters;

// One instance per endpoint, so the scoped IPermissionService comes from the request's services rather than the constructor.
// The team comes from the {teamId} route value, so the filter only goes on routes under /api/teams/{teamId:guid}.
internal sealed class TeamPermissionEndpointFilter(Permissions permission) : IEndpointFilter
{
    private const string TeamIdRouteValue = "teamId";
    private const string NotFoundMessage = "The team was not found.";

    private readonly Permissions _permission = permission;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var userId = httpContext.User.GetCallerId();
        if (!Guid.TryParse(httpContext.GetRouteValue(TeamIdRouteValue)?.ToString(), out var teamId))
        {
            throw new NotFoundException(NotFoundMessage);
        }

        var permissions = httpContext.RequestServices.GetRequiredService<IPermissionService>();
        var access = await permissions.CheckTeamPermissionAsync(userId, teamId, _permission, httpContext.RequestAborted);

        return access switch
        {
            TeamAccess.Granted => await next(context),
            TeamAccess.Forbidden => throw new ForbiddenException($"The {_permission} permission is required in this team."),
            _ => throw new NotFoundException(NotFoundMessage),
        };
    }
}
