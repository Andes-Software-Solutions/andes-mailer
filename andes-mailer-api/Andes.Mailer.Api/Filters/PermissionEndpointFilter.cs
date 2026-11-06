using Andes.Mailer.Common.Enums;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Security;

using Microsoft.Identity.Web;

namespace Andes.Mailer.Api.Filters;

// One instance per endpoint, so the scoped IPermissionService comes from the request's services rather than the constructor.
internal sealed class PermissionEndpointFilter(Permissions permission) : IEndpointFilter
{
    private readonly Permissions _permission = permission;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        if (!Guid.TryParse(httpContext.User.GetObjectId(), out var userId))
        {
            throw new ForbiddenException("The token does not identify a user.");
        }

        var permissions = httpContext.RequestServices.GetRequiredService<IPermissionService>();
        if (!await permissions.HasPermissionAsync(userId, _permission, httpContext.RequestAborted))
        {
            throw new ForbiddenException($"The {_permission} permission is required.");
        }

        return await next(context);
    }
}
