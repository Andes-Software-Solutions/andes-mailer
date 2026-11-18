using System.Security.Claims;

using Andes.Mailer.Api.Filters;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.ContactGroups;
using Andes.Mailer.Dto.ContactGroups;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Service.ContactGroups;
using Andes.Mailer.Service.Security;

using Microsoft.AspNetCore.Http.HttpResults;

namespace Andes.Mailer.Api.Endpoints;

internal static class ContactGroupEndpoints
{
    #region Private methods

    private static async Task<Ok<PaginatedResponseDto<ContactGroupSummaryDto>>> ListContactGroups(Guid teamId, [AsParameters] ListContactGroupsActionDto action, IContactGroupService groups, CancellationToken cancellationToken) =>
        TypedResults.Ok(await groups.ListAsync(teamId, action, cancellationToken));

    private static async Task<Ok<ContactGroupDto>> GetContactGroup(Guid teamId, Guid groupId, IContactGroupService groups, CancellationToken cancellationToken) =>
        TypedResults.Ok(await groups.GetAsync(teamId, groupId, cancellationToken));

    private static async Task<Created<ContactGroupDto>> CreateContactGroup(Guid teamId, CreateContactGroupActionDto action, ClaimsPrincipal caller, IContactGroupService groups, CancellationToken cancellationToken)
    {
        var group = await groups.CreateAsync(teamId, caller.GetCallerId(), action, cancellationToken);

        return TypedResults.Created($"/api/teams/{teamId}/contact-groups/{group.Id}", group);
    }

    private static async Task<Ok<ContactGroupDto>> UpdateContactGroup(Guid teamId, Guid groupId, UpdateContactGroupActionDto action, ClaimsPrincipal caller, IContactGroupService groups, CancellationToken cancellationToken) =>
        TypedResults.Ok(await groups.UpdateAsync(teamId, groupId, caller.GetCallerId(), action, cancellationToken));

    private static async Task<NoContent> DeactivateContactGroup(Guid teamId, Guid groupId, ClaimsPrincipal caller, IContactGroupService groups, CancellationToken cancellationToken)
    {
        await groups.DeactivateAsync(teamId, groupId, caller.GetCallerId(), cancellationToken);

        return TypedResults.NoContent();
    }

    #endregion

    #region Public static methods

    public static IEndpointRouteBuilder MapContactGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams/{teamId:guid}/contact-groups")
            .RequireAuthorization()
            .WithTags("Contact groups");

        // Reading needs only what composing an email needs, since every member adds groups to emails.
        group.MapGet("/", ListContactGroups)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ComposeTeamEmails))
            .AddEndpointFilter<ValidationEndpointFilter<ListContactGroupsActionDto>>()
            .WithName(nameof(ListContactGroups))
            .WithSummary("Lists one page of a team's contact groups, optionally those whose name contains some text.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{groupId:guid}", GetContactGroup)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ComposeTeamEmails))
            .WithName(nameof(GetContactGroup))
            .WithSummary("Gets a team's contact group with its members, ready to add to an email's recipients.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateContactGroup)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ManageTeamContactGroups))
            .AddEndpointFilter<ValidationEndpointFilter<CreateContactGroupActionDto>>()
            .WithName(nameof(CreateContactGroup))
            .WithSummary("Creates a contact group in a team.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{groupId:guid}", UpdateContactGroup)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ManageTeamContactGroups))
            .AddEndpointFilter<ValidationEndpointFilter<UpdateContactGroupActionDto>>()
            .WithName(nameof(UpdateContactGroup))
            .WithSummary("Replaces a contact group's name, description, and members.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{groupId:guid}", DeactivateContactGroup)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ManageTeamContactGroups))
            .WithName(nameof(DeactivateContactGroup))
            .WithSummary("Deactivates a contact group; emails it was added to keep their recipients.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    #endregion
}
