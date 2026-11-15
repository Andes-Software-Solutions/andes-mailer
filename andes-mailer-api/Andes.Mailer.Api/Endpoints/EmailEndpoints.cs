using System.Security.Claims;

using Andes.Mailer.Api.Filters;
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

    private static async Task<Ok<PaginatedResponseDto<EmailSummaryDto>>> ListEmails([AsParameters] ListEmailsActionDto action, ClaimsPrincipal caller, IEmailService emails, CancellationToken cancellationToken) =>
        TypedResults.Ok(await emails.ListAsync(caller.GetCallerId(), action, cancellationToken));

    private static async Task<Ok<EmailDto>> GetEmail(Guid id, ClaimsPrincipal caller, IEmailService emails, CancellationToken cancellationToken) =>
        TypedResults.Ok(await emails.GetAsync(id, caller.GetCallerId(), cancellationToken));

    private static async Task<Created<EmailDto>> CreateEmail(CreateEmailActionDto action, ClaimsPrincipal caller, IEmailService emails, CancellationToken cancellationToken)
    {
        var email = await emails.CreateAsync(caller.GetCallerId(), action, cancellationToken);

        return TypedResults.Created($"/api/emails/{email.Id}", email);
    }

    private static async Task<Ok<EmailDto>> UpdateEmail(Guid id, UpdateEmailActionDto action, ClaimsPrincipal caller, IEmailService emails, CancellationToken cancellationToken) =>
        TypedResults.Ok(await emails.UpdateAsync(id, caller.GetCallerId(), action, cancellationToken));

    private static async Task<NoContent> DeleteEmail(Guid id, ClaimsPrincipal caller, IEmailService emails, CancellationToken cancellationToken)
    {
        await emails.DeleteAsync(id, caller.GetCallerId(), cancellationToken);

        return TypedResults.NoContent();
    }

    #endregion

    #region Public static methods

    public static IEndpointRouteBuilder MapEmailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/emails")
            .RequireAuthorization()
            .WithTags("Emails");

        group.MapGet("/", ListEmails)
            .AddEndpointFilter<ValidationEndpointFilter<ListEmailsActionDto>>()
            .WithName(nameof(ListEmails))
            .WithSummary("Lists one page of the signed-in user's emails, most recently changed first.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", GetEmail)
            .WithName(nameof(GetEmail))
            .WithSummary("Gets one of the signed-in user's emails with its recipients and attachments.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateEmail)
            .AddEndpointFilter<ValidationEndpointFilter<CreateEmailActionDto>>()
            .WithName(nameof(CreateEmail))
            .WithSummary("Starts a draft email for the signed-in user.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/{id:guid}", UpdateEmail)
            .AddEndpointFilter<ValidationEndpointFilter<UpdateEmailActionDto>>()
            .WithName(nameof(UpdateEmail))
            .WithSummary("Replaces the subject, body, and recipients of one of the signed-in user's drafts.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteEmail)
            .WithName(nameof(DeleteEmail))
            .WithSummary("Deactivates one of the signed-in user's emails.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    #endregion
}
