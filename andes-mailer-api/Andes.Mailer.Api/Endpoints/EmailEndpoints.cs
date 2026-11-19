using System.Security.Claims;

using Andes.Mailer.Api.Filters;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Service.Emails;
using Andes.Mailer.Service.Emails.Sending;
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

    private static async Task<Created<EmailDto>> CreateEmailFromTemplate(Guid teamId, CreateEmailFromTemplateActionDto action, ClaimsPrincipal caller, IEmailService emails, CancellationToken cancellationToken)
    {
        var email = await emails.CreateFromTemplateAsync(teamId, caller.GetCallerId(), action, cancellationToken);

        return TypedResults.Created($"/api/teams/{teamId}/emails/{email.Id}", email);
    }

    private static async Task<Created<EmailDto>> DuplicateEmail(Guid teamId, Guid id, ClaimsPrincipal caller, IEmailService emails, CancellationToken cancellationToken)
    {
        var email = await emails.DuplicateAsync(teamId, id, caller.GetCallerId(), cancellationToken);

        return TypedResults.Created($"/api/teams/{teamId}/emails/{email.Id}", email);
    }

    private static async Task<Ok<EmailDto>> UpdateEmail(Guid teamId, Guid id, UpdateEmailActionDto action, IEmailService emails, CancellationToken cancellationToken) =>
        TypedResults.Ok(await emails.UpdateAsync(teamId, id, action, cancellationToken));

    private static async Task<NoContent> DeleteEmail(Guid teamId, Guid id, IEmailService emails, CancellationToken cancellationToken)
    {
        await emails.DeleteAsync(teamId, id, cancellationToken);

        return TypedResults.NoContent();
    }

    // Accepted, not Ok: the deliveries go out in the background, and the Location is where the client polls their progress.
    private static async Task<Accepted<EmailDto>> SendEmail(Guid teamId, Guid id, SendEmailActionDto action, ClaimsPrincipal caller, IEmailSendService sending, CancellationToken cancellationToken)
    {
        var email = await sending.SendAsync(teamId, id, caller.GetCallerId(), action, cancellationToken);

        return TypedResults.Accepted($"/api/teams/{teamId}/emails/{email.Id}/deliveries", email);
    }

    private static async Task<Ok<EmailDto>> UnscheduleEmail(Guid teamId, Guid id, EmailVersionActionDto action, IEmailSendService sending, CancellationToken cancellationToken) =>
        TypedResults.Ok(await sending.UnscheduleAsync(teamId, id, action, cancellationToken));

    private static async Task<Accepted<EmailDto>> RetryEmail(Guid teamId, Guid id, EmailVersionActionDto action, IEmailSendService sending, CancellationToken cancellationToken)
    {
        var email = await sending.RetryFailedAsync(teamId, id, action, cancellationToken);

        return TypedResults.Accepted($"/api/teams/{teamId}/emails/{email.Id}/deliveries", email);
    }

    private static async Task<Ok<IReadOnlyList<EmailDeliveryDto>>> ListEmailDeliveries(Guid teamId, Guid id, IEmailSendService sending, CancellationToken cancellationToken) =>
        TypedResults.Ok(await sending.ListDeliveriesAsync(teamId, id, cancellationToken));

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

        // Only the team check of the group: starting from a template is open to every member, like starting from scratch.
        group.MapPost("/from-template", CreateEmailFromTemplate)
            .AddEndpointFilter<ValidationEndpointFilter<CreateEmailFromTemplateActionDto>>()
            .WithName(nameof(CreateEmailFromTemplate))
            .WithSummary("Starts a draft email for a team from one of its templates.")
            .WithDescription("Copies the template's subject, body, recipients, and stored attachments. The draft is independent of the template from then on.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/duplicate", DuplicateEmail)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.DuplicateTeamEmails))
            .WithName(nameof(DuplicateEmail))
            .WithSummary("Starts a draft email for a team as a copy of another of its emails.")
            .WithDescription("Copies the email's subject, body, recipients, and stored attachments. The copy is always a draft, whether or not the source was sent.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

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

        group.MapPost("/{id:guid}/send", SendEmail)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.SendTeamEmails))
            .AddEndpointFilter<ValidationEndpointFilter<SendEmailActionDto>>()
            .WithName(nameof(SendEmail))
            .WithSummary("Sends one of a team's drafts now, or schedules it for later.")
            .WithDescription("Each recipient gets a message of their own from the team's service mailbox, sent in the background. Poll the deliveries for progress.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/unschedule", UnscheduleEmail)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.SendTeamEmails))
            .AddEndpointFilter<ValidationEndpointFilter<EmailVersionActionDto>>()
            .WithName(nameof(UnscheduleEmail))
            .WithSummary("Returns one of a team's scheduled emails to a draft.")
            .WithDescription("Allowed only until the dispatcher picks up the first recipient.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/retry", RetryEmail)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.SendTeamEmails))
            .AddEndpointFilter<ValidationEndpointFilter<EmailVersionActionDto>>()
            .WithName(nameof(RetryEmail))
            .WithSummary("Sends one of a team's emails again to the recipients it failed for.")
            .WithDescription("Each failed recipient gets a fresh set of attempts; recipients already sent are never sent again.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Only the team check of the group: every member can follow an email's progress.
        group.MapGet("/{id:guid}/deliveries", ListEmailDeliveries)
            .WithName(nameof(ListEmailDeliveries))
            .WithSummary("Lists the deliveries of one of a team's emails, one per recipient.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    #endregion
}
