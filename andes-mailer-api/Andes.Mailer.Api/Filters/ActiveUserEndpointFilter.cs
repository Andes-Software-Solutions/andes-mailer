using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Security;

using Microsoft.Identity.Web;

namespace Andes.Mailer.Api.Filters;

// For endpoints that need no permission but reach data beyond this application, such as the directory search: a valid token alone
// would let a deactivated user, or any guest the tenant issues tokens to, read through the API's app-only Graph access.
internal sealed class ActiveUserEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        if (!Guid.TryParse(httpContext.User.GetObjectId(), out var userId))
        {
            throw new ForbiddenException("The token does not identify a user.");
        }

        var permissions = httpContext.RequestServices.GetRequiredService<IPermissionService>();
        return !await permissions.IsActiveUserAsync(userId, httpContext.RequestAborted)
            ? throw new ForbiddenException("Only active users of the application can do this.")
            : await next(context);
    }
}
