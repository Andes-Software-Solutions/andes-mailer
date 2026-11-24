using System.Security.Claims;

using Andes.Mailer.Api.Filters;
using Andes.Mailer.Api.Idempotency;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Teams;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Dto.Teams;
using Andes.Mailer.Service.Security;
using Andes.Mailer.Service.Teams;

using Microsoft.AspNetCore.Http.HttpResults;

namespace Andes.Mailer.Api.Endpoints;

internal static class TeamEndpoints
{
    #region Private methods

    private static async Task<Ok<PaginatedResponseDto<TeamSummaryDto>>> ListTeams([AsParameters] ListTeamsActionDto action, ClaimsPrincipal caller, ITeamService teams, CancellationToken cancellationToken) =>
        TypedResults.Ok(await teams.ListAsync(caller.GetCallerId(), action, cancellationToken));

    private static async Task<Ok<TeamDto>> GetTeam(Guid teamId, ITeamService teams, CancellationToken cancellationToken) =>
        TypedResults.Ok(await teams.GetAsync(teamId, cancellationToken));

    private static async Task<Created<TeamDto>> CreateTeam(CreateTeamActionDto action, ITeamService teams, CancellationToken cancellationToken)
    {
        var team = await teams.CreateAsync(action, cancellationToken);

        return TypedResults.Created($"/api/teams/{team.Id}", team);
    }

    private static async Task<Ok<TeamDto>> UpdateTeam(Guid teamId, UpdateTeamActionDto action, ClaimsPrincipal caller, ITeamService teams, CancellationToken cancellationToken) =>
        TypedResults.Ok(await teams.UpdateAsync(caller.GetCallerId(), teamId, action, cancellationToken));

    private static async Task<NoContent> DeactivateTeam(Guid teamId, ITeamService teams, CancellationToken cancellationToken)
    {
        await teams.DeactivateAsync(teamId, cancellationToken);

        return TypedResults.NoContent();
    }

    #endregion

    #region Public static methods

    public static IEndpointRouteBuilder MapTeamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams")
            .RequireAuthorization()
            .WithTags("Teams");

        group.MapGet("/", ListTeams)
            .AddEndpointFilter<ValidationEndpointFilter<ListTeamsActionDto>>()
            .WithName(nameof(ListTeams))
            .WithSummary("Lists one page of the teams the signed-in user can see, with their role in each.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{teamId:guid}", GetTeam)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ViewTeam))
            .WithName(nameof(GetTeam))
            .WithSummary("Gets a team.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateTeam)
            .WithIdempotency()
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.CreateTeam))
            .AddEndpointFilter<ValidationEndpointFilter<CreateTeamActionDto>>()
            .WithName(nameof(CreateTeam))
            .WithSummary("Creates a team that sends from a directory mailbox.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{teamId:guid}", UpdateTeam)
            .WithIdempotency()
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ModifyTeam))
            .AddEndpointFilter<ValidationEndpointFilter<UpdateTeamActionDto>>()
            .WithName(nameof(UpdateTeam))
            .WithSummary("Replaces a team's details and settings; only a global administrator may change its service account.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{teamId:guid}", DeactivateTeam)
            .WithIdempotency()
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.DeactivateTeam))
            .WithName(nameof(DeactivateTeam))
            .WithSummary("Deactivates a team, hiding its members and emails.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    #endregion
}
