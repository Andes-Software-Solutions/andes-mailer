using Andes.Mailer.Api.Filters;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Teams;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Dto.Teams;
using Andes.Mailer.Service.Teams;

using Microsoft.AspNetCore.Http.HttpResults;

namespace Andes.Mailer.Api.Endpoints;

internal static class TeamMemberEndpoints
{
    #region Private methods

    private static async Task<Ok<PaginatedResponseDto<TeamMemberDto>>> ListTeamMembers(Guid teamId, [AsParameters] ListTeamMembersActionDto action, ITeamMemberService members, CancellationToken cancellationToken) =>
        TypedResults.Ok(await members.ListAsync(teamId, action, cancellationToken));

    private static async Task<Ok<TeamMemberDto>> GetTeamMember(Guid teamId, Guid userId, ITeamMemberService members, CancellationToken cancellationToken) =>
        TypedResults.Ok(await members.GetAsync(teamId, userId, cancellationToken));

    private static async Task<Created<TeamMemberDto>> AddTeamMember(Guid teamId, AddTeamMemberActionDto action, ITeamMemberService members, CancellationToken cancellationToken)
    {
        var member = await members.AddAsync(teamId, action, cancellationToken);

        return TypedResults.Created($"/api/teams/{teamId}/members/{member.UserId}", member);
    }

    private static async Task<Ok<TeamMemberDto>> UpdateTeamMember(Guid teamId, Guid userId, UpdateTeamMemberActionDto action, ITeamMemberService members, CancellationToken cancellationToken) =>
        TypedResults.Ok(await members.UpdateRoleAsync(teamId, userId, action, cancellationToken));

    private static async Task<NoContent> RemoveTeamMember(Guid teamId, Guid userId, ITeamMemberService members, CancellationToken cancellationToken)
    {
        await members.RemoveAsync(teamId, userId, cancellationToken);

        return TypedResults.NoContent();
    }

    #endregion

    #region Public static methods

    public static IEndpointRouteBuilder MapTeamMemberEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams/{teamId:guid}/members")
            .RequireAuthorization()
            .WithTags("Team members");

        group.MapGet("/", ListTeamMembers)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ViewTeam))
            .AddEndpointFilter<ValidationEndpointFilter<ListTeamMembersActionDto>>()
            .WithName(nameof(ListTeamMembers))
            .WithSummary("Lists one page of a team's members and their roles.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{userId:guid}", GetTeamMember)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ViewTeam))
            .WithName(nameof(GetTeamMember))
            .WithSummary("Gets a member of a team and their role.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", AddTeamMember)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.CreateTeamUser))
            .AddEndpointFilter<ValidationEndpointFilter<AddTeamMemberActionDto>>()
            .WithName(nameof(AddTeamMember))
            .WithSummary("Adds a person from the Entra directory to a team by email, adding them as a user if needed.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{userId:guid}", UpdateTeamMember)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ModifyTeamUser))
            .AddEndpointFilter<ValidationEndpointFilter<UpdateTeamMemberActionDto>>()
            .WithName(nameof(UpdateTeamMember))
            .WithSummary("Changes the role a member holds in a team.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{userId:guid}", RemoveTeamMember)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.DeactivateTeamUser))
            .WithName(nameof(RemoveTeamMember))
            .WithSummary("Removes a member from a team; the user stays in their other teams.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    #endregion
}
