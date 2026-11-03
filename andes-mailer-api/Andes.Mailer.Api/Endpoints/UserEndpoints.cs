using System.Security.Claims;

using Andes.Mailer.Dto.Users;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Identity.Web;

namespace Andes.Mailer.Api.Endpoints;

internal static class UserEndpoints
{
    #region Private methods

    // The name claim first: GetDisplayName prefers preferred_username, the sign-in address.
    private static Ok<CurrentUserDto> GetCurrentUser(ClaimsPrincipal user) =>
        TypedResults.Ok(new CurrentUserDto(
            user.GetObjectId() ?? string.Empty,
            user.FindFirstValue(ClaimConstants.Name) ?? user.GetDisplayName() ?? string.Empty));

    #endregion

    #region Public static methods

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .RequireAuthorization()
            .WithTags("Users");

        group.MapGet("/me", GetCurrentUser)
            .WithName(nameof(GetCurrentUser))
            .WithSummary("Gets the signed-in user.");

        return app;
    }

    #endregion
}
