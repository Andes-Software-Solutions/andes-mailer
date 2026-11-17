using Andes.Mailer.Dto.Actions.Emails;
using Andes.Mailer.Dto.Actions.Emails.Templates;
using Andes.Mailer.Dto.Emails.Templates;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Entity.Emails.Templates;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Exceptions;

using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Service.Emails.Templates;

/// <summary>
/// Manages the email templates of a team. Every operation is scoped to one team, which the caller has already been checked against.
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>
    /// Lists one page of a team's templates, ordered by name.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="action">The page to return.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The page.</returns>
    Task<PaginatedResponseDto<EmailTemplateSummaryDto>> ListAsync(Guid teamId, ListEmailTemplatesActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one of a team's templates with its recipients and attachments.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="id">The template's id.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The template.</returns>
    /// <exception cref="NotFoundException">The team has no active template with the id.</exception>
    Task<EmailTemplateDto> GetAsync(Guid teamId, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a template for a team.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="authorId">The Entra object id of the signed-in user, who must be an active user.</param>
    /// <param name="action">The name, subject, body, and recipients.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The new template.</returns>
    /// <exception cref="ConflictException">Another active template of the team has the name.</exception>
    Task<EmailTemplateDto> CreateAsync(Guid teamId, Guid authorId, CreateEmailTemplateActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the name, subject, body, and recipients of one of a team's templates. Emails already made from it do not change.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="id">The template's id.</param>
    /// <param name="action">The new content and the row version the caller last read.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated template.</returns>
    /// <exception cref="NotFoundException">The team has no active template with the id.</exception>
    /// <exception cref="ConflictException">Another active template of the team has the name, or the template changed since the caller read it.</exception>
    Task<EmailTemplateDto> UpdateAsync(Guid teamId, Guid id, UpdateEmailTemplateActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Deactivates one of a team's templates. Its attachments stay in storage, and emails already made from it do not change.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="id">The template's id.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the template is deactivated.</returns>
    /// <exception cref="NotFoundException">The team has no active template with the id.</exception>
    /// <exception cref="ConflictException">The template changed during the operation.</exception>
    Task DeleteAsync(Guid teamId, Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Manages the email templates of a team in the SQL store.
/// </summary>
/// <param name="ctx">The store.</param>
public sealed class EmailTemplateService(AndesMailerDbContext ctx) : IEmailTemplateService
{
    // One message for "missing" and "another team's", so the API never confirms that another team's template exists.
    private const string NotFoundMessage = "The template was not found.";
    private const string NameTakenMessage = "The team already has a template with this name.";

    private readonly AndesMailerDbContext _ctx = ctx;

    /// <inheritdoc/>
    public async Task<PaginatedResponseDto<EmailTemplateSummaryDto>> ListAsync(Guid teamId, ListEmailTemplatesActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var templates = _ctx.EmailTemplates.AsNoTracking().Where(t => t.TeamId == teamId);
        var total = await templates.CountAsync(cancellationToken);
        var items = await templates
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Id)
            .Skip((action.Page - 1) * action.PageSize)
            .Take(action.PageSize)
            .Select(EmailTemplateMapper.MapToEmailTemplateSummaryDtoExpression)
            .ToListAsync(cancellationToken);

        return new PaginatedResponseDto<EmailTemplateSummaryDto>(items, action.Page, action.PageSize, total);
    }

    /// <inheritdoc/>
    public async Task<EmailTemplateDto> GetAsync(Guid teamId, Guid id, CancellationToken cancellationToken)
    {
        var template = await _ctx.EmailTemplates
            .AsNoTracking()
            .Where(t => t.Id == id && t.TeamId == teamId)
            .Select(EmailTemplateMapper.MapToEmailTemplateDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);

        return template ?? throw new NotFoundException(NotFoundMessage);
    }

    /// <inheritdoc/>
    public async Task<EmailTemplateDto> CreateAsync(Guid teamId, Guid authorId, CreateEmailTemplateActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var template = new EmailTemplate
        {
            TeamId = teamId,
            AuthorId = authorId,
            Name = action.Name.Trim(),
            Subject = action.Subject.Trim(),
            BodyHtml = EmailBodySanitizer.Sanitize(action.BodyHtml),
        };
        ApplyRecipients(template, action.Recipients);
        await EnsureNameAvailableAsync(template, cancellationToken);
        _ctx.Add(template);
        await SaveAsync(template, cancellationToken);

        return EmailTemplateMapper.MapToEmailTemplateDto(template);
    }

    /// <inheritdoc/>
    public async Task<EmailTemplateDto> UpdateAsync(Guid teamId, Guid id, UpdateEmailTemplateActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var template = await _ctx.EmailTemplates
            .Include(t => t.Recipients)
            .Include(t => t.Attachments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id && t.TeamId == teamId, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);

        // The version the client last read, so the UPDATE matches no row if anyone changed the template since.
        _ctx.Entry(template).Property(t => t.Version).OriginalValue = Convert.FromBase64String(action.Version);
        template.Name = action.Name.Trim();
        template.Subject = action.Subject.Trim();
        template.BodyHtml = EmailBodySanitizer.Sanitize(action.BodyHtml);
        ApplyRecipients(template, action.Recipients);
        await EnsureNameAvailableAsync(template, cancellationToken);

        // A recipients-only change touches no column of the template itself; marking it modified keeps the version check and bumps the version.
        _ctx.Entry(template).State = EntityState.Modified;
        await SaveAsync(template, cancellationToken);

        return EmailTemplateMapper.MapToEmailTemplateDto(template);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid teamId, Guid id, CancellationToken cancellationToken)
    {
        var template = await _ctx.EmailTemplates.FirstOrDefaultAsync(t => t.Id == id && t.TeamId == teamId, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);

        // DeactivationInterceptor turns the delete into a DateDeactivated stamp; the recipient and attachment filters hide the children with it.
        _ctx.Remove(template);
        await SaveAsync(null, cancellationToken);
    }

    #region Private methods

    // Keeps the rows of addresses that stay, so unchanged recipients are not rewritten.
    private void ApplyRecipients(EmailTemplate template, IReadOnlyList<EmailRecipientActionDto> recipients)
    {
        var wanted = recipients
            .Select((r, position) => (Address: r.Address.Trim().ToLowerInvariant(), r.Type, Position: position))
            .ToList();
        var existing = template.Recipients.ToDictionary(r => r.Address, StringComparer.Ordinal);
        var addresses = wanted.Select(w => w.Address).ToHashSet(StringComparer.Ordinal);
        foreach (var stale in existing.Values.Where(r => !addresses.Contains(r.Address)).ToList())
        {
            template.Recipients.Remove(stale);
            _ctx.Remove(stale);
        }

        foreach (var (address, type, position) in wanted)
        {
            if (existing.TryGetValue(address, out var recipient))
            {
                recipient.RecipientTypeId = type;
                recipient.Position = position;
                continue;
            }

            template.Recipients.Add(new EmailTemplateRecipient { Address = address, RecipientTypeId = type, Position = position });
        }
    }

    // A fresh query, not the tracker, so it sees only what is committed, never the template being saved.
    private async Task EnsureNameAvailableAsync(EmailTemplate template, CancellationToken cancellationToken)
    {
        if (await _ctx.EmailTemplates.AsNoTracking().AnyAsync(t => t.TeamId == template.TeamId && t.Id != template.Id && t.Name == template.Name, cancellationToken))
        {
            throw new ConflictException(NameTakenMessage);
        }
    }

    private async Task SaveAsync(EmailTemplate? saved, CancellationToken cancellationToken)
    {
        try
        {
            await _ctx.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The template was changed by another request. Read it again and retry.");
        }
        catch (DbUpdateException) when (saved is not null)
        {
            // A parallel save took the name between the check and this save. Re-checked rather than read from the provider's error number,
            // which would bring SQL Server types into Service; any other store fault propagates, so GlobalExceptionHandler logs it as a 500.
            await EnsureNameAvailableAsync(saved, cancellationToken);

            throw;
        }
    }

    #endregion
}
