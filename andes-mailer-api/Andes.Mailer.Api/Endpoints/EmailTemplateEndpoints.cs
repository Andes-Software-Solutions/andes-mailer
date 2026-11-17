using System.Security.Claims;
using Andes.Mailer.Api.Filters;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails.Templates;
using Andes.Mailer.Dto.Emails.Templates;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Service.Emails.Templates;
using Andes.Mailer.Service.Security;

using Microsoft.AspNetCore.Http.HttpResults;

namespace Andes.Mailer.Api.Endpoints;

internal static class EmailTemplateEndpoints
{
    #region Private methods

    private static async Task<Ok<PaginatedResponseDto<EmailTemplateSummaryDto>>> ListEmailTemplates(Guid teamId, [AsParameters] ListEmailTemplatesActionDto action, IEmailTemplateService templates, CancellationToken cancellationToken) =>
        TypedResults.Ok(await templates.ListAsync(teamId, action, cancellationToken));

    private static async Task<Ok<EmailTemplateDto>> GetEmailTemplate(Guid teamId, Guid id, IEmailTemplateService templates, CancellationToken cancellationToken) =>
        TypedResults.Ok(await templates.GetAsync(teamId, id, cancellationToken));

    private static async Task<Created<EmailTemplateDto>> CreateEmailTemplate(Guid teamId, CreateEmailTemplateActionDto action, ClaimsPrincipal caller, IEmailTemplateService templates, CancellationToken cancellationToken)
    {
        var template = await templates.CreateAsync(teamId, caller.GetCallerId(), action, cancellationToken);

        return TypedResults.Created($"/api/teams/{teamId}/templates/{template.Id}", template);
    }

    private static async Task<Ok<EmailTemplateDto>> UpdateEmailTemplate(Guid teamId, Guid id, UpdateEmailTemplateActionDto action, IEmailTemplateService templates, CancellationToken cancellationToken) =>
        TypedResults.Ok(await templates.UpdateAsync(teamId, id, action, cancellationToken));

    private static async Task<NoContent> DeleteEmailTemplate(Guid teamId, Guid id, IEmailTemplateService templates, CancellationToken cancellationToken)
    {
        await templates.DeleteAsync(teamId, id, cancellationToken);

        return TypedResults.NoContent();
    }

    #endregion

    #region Public static methods

    public static IEndpointRouteBuilder MapEmailTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        // Every member of the team reads templates to start emails from them; only a role with ManageTeamTemplates changes them.
        var group = app.MapGroup("/api/teams/{teamId:guid}/templates")
            .RequireAuthorization()
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ComposeTeamEmails))
            .WithTags("Email templates");

        group.MapGet("/", ListEmailTemplates)
            .AddEndpointFilter<ValidationEndpointFilter<ListEmailTemplatesActionDto>>()
            .WithName(nameof(ListEmailTemplates))
            .WithSummary("Lists one page of a team's email templates, ordered by name.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", GetEmailTemplate)
            .WithName(nameof(GetEmailTemplate))
            .WithSummary("Gets one of a team's email templates with its recipients and attachments.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateEmailTemplate)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ManageTeamTemplates))
            .AddEndpointFilter<ValidationEndpointFilter<CreateEmailTemplateActionDto>>()
            .WithName(nameof(CreateEmailTemplate))
            .WithSummary("Creates an email template for a team.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", UpdateEmailTemplate)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ManageTeamTemplates))
            .AddEndpointFilter<ValidationEndpointFilter<UpdateEmailTemplateActionDto>>()
            .WithName(nameof(UpdateEmailTemplate))
            .WithSummary("Replaces the name, subject, body, and recipients of one of a team's email templates.")
            .WithDescription("Emails already made from the template keep the content they were made with.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteEmailTemplate)
            .AddEndpointFilter(new TeamPermissionEndpointFilter(Permissions.ManageTeamTemplates))
            .WithName(nameof(DeleteEmailTemplate))
            .WithSummary("Deactivates one of a team's email templates.")
            .WithDescription("Emails already made from the template keep their content and their own copies of its attachments.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    #endregion
}
