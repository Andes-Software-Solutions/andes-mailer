using Andes.Mailer.Api.Filters;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Service.Emails.Attachments;

using Microsoft.AspNetCore.Http.HttpResults;

namespace Andes.Mailer.Api.Endpoints;

internal static class EmailAttachmentEndpoints
{
    #region Private methods

    private static async Task<Accepted<EmailAttachmentDto>> UploadAttachment(Guid teamId, Guid emailId, IFormFile file, IAttachmentService attachments, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var attachment = await attachments.StageAsync(teamId, emailId, file.FileName, file.Length, content, cancellationToken);

        return TypedResults.Accepted($"/api/teams/{teamId}/emails/{emailId}/attachments/{attachment.Id}", attachment);
    }

    private static async Task<Ok<EmailAttachmentDto>> GetAttachment(Guid teamId, Guid emailId, Guid id, IAttachmentService attachments, CancellationToken cancellationToken) =>
        TypedResults.Ok(await attachments.GetAsync(teamId, emailId, id, cancellationToken));

    // A download name makes the response Content-Disposition: attachment, so a browser saves the file instead of rendering it in the app's origin.
    private static async Task<FileStreamHttpResult> DownloadAttachment(Guid teamId, Guid emailId, Guid id, IAttachmentService attachments, CancellationToken cancellationToken)
    {
        var attachment = await attachments.OpenContentAsync(teamId, emailId, id, cancellationToken);

        return TypedResults.Stream(attachment.Content, attachment.ContentType, attachment.FileName);
    }

    private static async Task<NoContent> DeleteAttachment(Guid teamId, Guid emailId, Guid id, IAttachmentService attachments, CancellationToken cancellationToken)
    {
        await attachments.DeleteAsync(teamId, emailId, id, cancellationToken);

        return TypedResults.NoContent();
    }

    #endregion

    #region Public static methods

    public static IEndpointRouteBuilder MapEmailAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams/{teamId:guid}/emails/{emailId:guid}/attachments")
            .RequireAuthorization()
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ComposeTeamEmails))
            .WithTags("Email attachments");

        // Antiforgery is off because the API authenticates with bearer tokens, never cookies, so a cross-site form cannot carry credentials.
        // Kestrel's default 30,000,000-byte request limit already sits just above the 25 MB file cap, so no per-endpoint limit is set.
        group.MapPost("/", UploadAttachment)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.CanAttachDocuments))
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .WithName(nameof(UploadAttachment))
            .WithSummary("Receives a document for a draft and uploads it to storage in the background.")
            .WithDescription("Returns 202 with the attachment in status Pending. Poll the Location until the status is Uploaded or Failed. Returns 409 when the email is no longer a draft, or already has the same document attached, whatever its file name.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", GetAttachment)
            .WithName(nameof(GetAttachment))
            .WithSummary("Gets an attachment and the progress of its upload.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/content", DownloadAttachment)
            .WithName(nameof(DownloadAttachment))
            .WithSummary("Downloads a stored attachment.")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteAttachment)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.CanAttachDocuments))
            .WithName(nameof(DeleteAttachment))
            .WithSummary("Removes an attachment from a draft and deletes its document from storage.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    #endregion
}
