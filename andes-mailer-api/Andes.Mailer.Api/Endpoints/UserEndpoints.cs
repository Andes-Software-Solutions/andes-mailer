using System.Security.Claims;

using Andes.Mailer.Api.Filters;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Users;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Dto.Users;
using Andes.Mailer.Service.Users;

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

    private static async Task<Ok<PaginatedResponseDto<UserDto>>> ListUsers([AsParameters] ListUsersActionDto action, IUserService users, CancellationToken cancellationToken) =>
        TypedResults.Ok(await users.ListAsync(action, cancellationToken));

    private static async Task<Ok<UserDto>> GetUser(Guid id, IUserService users, CancellationToken cancellationToken) =>
        TypedResults.Ok(await users.GetAsync(id, cancellationToken));

    private static async Task<Created<UserDto>> CreateUser(CreateUserActionDto action, IUserService users, CancellationToken cancellationToken)
    {
        var user = await users.CreateAsync(action, cancellationToken);

        return TypedResults.Created($"/api/users/{user.Id}", user);
    }

    private static async Task<Ok<UserDto>> UpdateUser(Guid id, UpdateUserActionDto action, IUserService users, CancellationToken cancellationToken) =>
        TypedResults.Ok(await users.UpdateAsync(id, action, cancellationToken));

    // PermissionEndpointFilter has already parsed the oid, so an unparsable claim never reaches here.
    private static async Task<NoContent> DeactivateUser(Guid id, ClaimsPrincipal caller, IUserService users, CancellationToken cancellationToken)
    {
        await users.DeactivateAsync(id, Guid.Parse(caller.GetObjectId()!), cancellationToken);

        return TypedResults.NoContent();
    }

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

        group.MapGet("/", ListUsers)
            .AddEndpointFilter<ValidationEndpointFilter<ListUsersActionDto>>()
            .WithName(nameof(ListUsers))
            .WithSummary("Lists one page of active users.")
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetUser)
            .WithName(nameof(GetUser))
            .WithSummary("Gets an active user.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateUser)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.CreateUser))
            .AddEndpointFilter<ValidationEndpointFilter<CreateUserActionDto>>()
            .WithName(nameof(CreateUser))
            .WithSummary("Adds a user from the Entra directory by email, or reactivates them.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", UpdateUser)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.ModifyUser))
            .AddEndpointFilter<ValidationEndpointFilter<UpdateUserActionDto>>()
            .WithName(nameof(UpdateUser))
            .WithSummary("Changes a user's name.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeactivateUser)
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.DeactivateUser))
            .WithName(nameof(DeactivateUser))
            .WithSummary("Deactivates a user.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    #endregion
}
