using System.Security.Claims;

using Andes.Mailer.Api.Filters;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Service.Emails;
using Andes.Mailer.Service.Security;

using Microsoft.AspNetCore.Http.HttpResults;

namespace Andes.Mailer.Api.Endpoints;

internal static class EmailEndpoints
{
    #region Private methods

    private static async Task<Ok<PaginatedResponseDto<EmailSummaryDto>>> ListEmails(Guid teamId, [AsParameters] ListEmailsActionDto action, IEmailService emails, CancellationToken cancellationToken) =>
        TypedResults.Ok(await emails.ListAsync(teamId, action, cancellationToken));

    private static async Task<Ok<EmailDto>> GetEmail(Guid teamId, Guid id, IEmailService emails, CancellationToken cancellationToken) =>
        TypedResults.Ok(await emails.GetAsync(teamId, id, cancellationToken));

    private static async Task<Created<EmailDto>> CreateEmail(Guid teamId, CreateEmailActionDto action, ClaimsPrincipal caller, IEmailService emails, CancellationToken cancellationToken)
    {
        var email = await emails.CreateAsync(teamId, caller.GetCallerId(), action, cancellationToken);

        return TypedResults.Created($"/api/teams/{teamId}/emails/{email.Id}", email);
    }

    private static async Task<Ok<EmailDto>> UpdateEmail(Guid teamId, Guid id, UpdateEmailActionDto action, IEmailService emails, CancellationToken cancellationToken) =>
        TypedResults.Ok(await emails.UpdateAsync(teamId, id, action, cancellationToken));

    private static async Task<NoContent> DeleteEmail(Guid teamId, Guid id, IEmailService emails, CancellationToken cancellationToken)
    {
        await emails.DeleteAsync(teamId, id, cancellationToken);

        return TypedResults.NoContent();
    }

    #endregion

    #region Public static methods

    public static IEndpointRouteBuilder MapEmailEndpoints(this IEndpointRouteBuilder app)
    {
        // Every member of the team can read and change every one of its emails, so one team check covers the whole group.
        var group = app.MapGroup("/api/teams/{teamId:guid}/emails")
            .RequireAuthorization()
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ComposeTeamEmails))
            .WithTags("Emails");

        group.MapGet("/", ListEmails)
            .AddEndpointFilter<ValidationEndpointFilter<ListEmailsActionDto>>()
            .WithName(nameof(ListEmails))
            .WithSummary("Lists one page of a team's emails, most recently changed first.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", GetEmail)
            .WithName(nameof(GetEmail))
            .WithSummary("Gets one of a team's emails with its recipients and attachments.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateEmail)
            .AddEndpointFilter<ValidationEndpointFilter<CreateEmailActionDto>>()
            .WithName(nameof(CreateEmail))
            .WithSummary("Starts a draft email for a team.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateEmail)
            .AddEndpointFilter<ValidationEndpointFilter<UpdateEmailActionDto>>()
            .WithName(nameof(UpdateEmail))
            .WithSummary("Replaces the subject, body, and recipients of one of a team's drafts.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteEmail)
            .WithName(nameof(DeleteEmail))
            .WithSummary("Deactivates one of a team's emails.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    #endregion
}
