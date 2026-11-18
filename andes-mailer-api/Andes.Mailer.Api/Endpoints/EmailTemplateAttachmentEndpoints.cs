using Andes.Mailer.Api.Filters;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Service.Emails.Templates.Attachments;

using Microsoft.AspNetCore.Http.HttpResults;

namespace Andes.Mailer.Api.Endpoints;

internal static class EmailTemplateAttachmentEndpoints
{
    #region Private methods

    private static async Task<Accepted<EmailAttachmentDto>> UploadTemplateAttachment(Guid teamId, Guid templateId, IFormFile file, ITemplateAttachmentService attachments, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var attachment = await attachments.StageAsync(teamId, templateId, file.FileName, file.Length, content, cancellationToken);

        return TypedResults.Accepted($"/api/teams/{teamId}/templates/{templateId}/attachments/{attachment.Id}", attachment);
    }

    private static async Task<Ok<EmailAttachmentDto>> GetTemplateAttachment(Guid teamId, Guid templateId, Guid id, ITemplateAttachmentService attachments, CancellationToken cancellationToken) =>
        TypedResults.Ok(await attachments.GetAsync(teamId, templateId, id, cancellationToken));

    // A download name makes the response Content-Disposition: attachment, so a browser saves the file instead of rendering it in the app's origin.
    private static async Task<FileStreamHttpResult> DownloadTemplateAttachment(Guid teamId, Guid templateId, Guid id, ITemplateAttachmentService attachments, CancellationToken cancellationToken)
    {
        var attachment = await attachments.OpenContentAsync(teamId, templateId, id, cancellationToken);

        return TypedResults.Stream(attachment.Content, attachment.ContentType, attachment.FileName);
    }

    private static async Task<NoContent> DeleteTemplateAttachment(Guid teamId, Guid templateId, Guid id, ITemplateAttachmentService attachments, CancellationToken cancellationToken)
    {
        await attachments.DeleteAsync(teamId, templateId, id, cancellationToken);

        return TypedResults.NoContent();
    }

    #endregion

    #region Public static methods

    public static IEndpointRouteBuilder MapEmailTemplateAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams/{teamId:guid}/templates/{templateId:guid}/attachments")
            .RequireAuthorization()
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ComposeTeamEmails))
            .WithTags("Email template attachments");

        // Antiforgery is off because the API authenticates with bearer tokens, never cookies, so a cross-site form cannot carry credentials.
        // Kestrel's default 30,000,000-byte request limit already sits just above the 25 MB file cap, so no per-endpoint limit is set.
        group.MapPost("/", UploadTemplateAttachment)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ManageTeamTemplates))
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.CanAttachDocuments))
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .WithName(nameof(UploadTemplateAttachment))
            .WithSummary("Receives a document for an email template and uploads it to storage in the background.")
            .WithDescription("Returns 202 with the attachment in status Pending. Poll the Location until the status is Uploaded or Failed. Returns 409 when the template already has the same document attached, whatever its file name.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", GetTemplateAttachment)
            .WithName(nameof(GetTemplateAttachment))
            .WithSummary("Gets an email template's attachment and the progress of its upload.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/content", DownloadTemplateAttachment)
            .WithName(nameof(DownloadTemplateAttachment))
            .WithSummary("Downloads a stored attachment of an email template.")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteTemplateAttachment)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ManageTeamTemplates))
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.CanAttachDocuments))
            .WithName(nameof(DeleteTemplateAttachment))
            .WithSummary("Removes an attachment from an email template and deletes its document from storage.")
            .WithDescription("Emails already made from the template keep their own copies of the document.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    #endregion
}
