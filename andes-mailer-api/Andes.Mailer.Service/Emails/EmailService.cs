using Andes.Mailer.Common.Constants.Emails;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Entity.Emails.Interfaces;
using Andes.Mailer.Entity.Users;
using Andes.Mailer.Repository.Blob;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Emails.Attachments;
using Andes.Mailer.Service.Emails.Exceptions;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Andes.Mailer.Service.Emails;

/// <summary>
/// Manages the emails teams compose. Every operation is scoped to one team, which the caller has already been checked against.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Lists one page of a team's emails, most recently changed first.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="action">The page to return.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The page.</returns>
    Task<PaginatedResponseDto<EmailSummaryDto>> ListAsync(Guid teamId, ListEmailsActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one of a team's emails with its recipients and attachments.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="id">The email's id.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The email.</returns>
    /// <exception cref="NotFoundException">The team has no active email with the id.</exception>
    Task<EmailDto> GetAsync(Guid teamId, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Starts a draft email for a team.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="authorId">The Entra object id of the signed-in user, who must be an active user.</param>
    /// <param name="action">The subject, body, and recipients.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The new draft.</returns>
    Task<EmailDto> CreateAsync(Guid teamId, Guid authorId, CreateEmailActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Starts a draft email for a team from one of its templates, copying the template's subject, body, recipients, and stored attachments.
    /// </summary>
    /// <remarks>
    /// The draft is independent of the template from then on: it has its own copy of every document, and later changes to the template
    /// never reach it. Attachments whose upload failed are left out.
    /// </remarks>
    /// <param name="teamId">The team's id.</param>
    /// <param name="authorId">The Entra object id of the signed-in user, who must be an active user.</param>
    /// <param name="action">The template to start from.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The new draft.</returns>
    /// <exception cref="NotFoundException">The team has no active template with the id, or storage no longer holds one of its documents.</exception>
    /// <exception cref="TemplateNotReadyException">One of the template's attachments is still uploading.</exception>
    Task<EmailDto> CreateFromTemplateAsync(Guid teamId, Guid authorId, CreateEmailFromTemplateActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Starts a draft email for a team as a copy of another of its emails, copying its subject, body, recipients, and stored attachments.
    /// </summary>
    /// <remarks>
    /// The source may be a draft or already sent; the copy is always a draft by the caller, made from the same template as the source if
    /// any. It has its own copy of every document. Attachments whose upload failed are left out.
    /// </remarks>
    /// <param name="teamId">The team's id.</param>
    /// <param name="id">The id of the email to copy.</param>
    /// <param name="authorId">The Entra object id of the signed-in user, who must be an active user.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The new draft.</returns>
    /// <exception cref="NotFoundException">The team has no active email with the id, or storage no longer holds one of its documents.</exception>
    /// <exception cref="EmailNotReadyException">One of the email's attachments is still uploading.</exception>
    Task<EmailDto> DuplicateAsync(Guid teamId, Guid id, Guid authorId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the subject, body, and recipients of one of a team's drafts.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="id">The email's id.</param>
    /// <param name="action">The new content and the row version the caller last read.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated draft.</returns>
    /// <exception cref="NotFoundException">The team has no active email with the id.</exception>
    /// <exception cref="EmailNotEditableException">The email is no longer a draft.</exception>
    /// <exception cref="ConflictException">The email changed since the caller read it.</exception>
    Task<EmailDto> UpdateAsync(Guid teamId, Guid id, UpdateEmailActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Deactivates one of a team's emails. Its attachments stay in storage.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="id">The email's id.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the email is deactivated.</returns>
    /// <exception cref="NotFoundException">The team has no active email with the id.</exception>
    /// <exception cref="ConflictException">The email changed during the operation.</exception>
    Task DeleteAsync(Guid teamId, Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Manages the emails teams compose in the SQL store, resolving recipients against the directory.
/// </summary>
/// <param name="ctx">The store.</param>
/// <param name="directory">The directory recipients are resolved against.</param>
/// <param name="store">The blob storage a template's or email's documents are copied in.</param>
/// <param name="logger">The logger.</param>
public sealed class EmailService(AndesMailerDbContext ctx, IDirectoryUserService directory, IAttachmentStore store, ILogger<EmailService> logger) : IEmailService
{
    // One message for "missing" and "another team's", so the API never confirms that another team's email exists.
    private const string NotFoundMessage = "The email was not found.";
    private const string TemplateNotFoundMessage = "The template was not found.";

    private readonly AndesMailerDbContext _ctx = ctx;
    private readonly IDirectoryUserService _directory = directory;
    private readonly IAttachmentStore _store = store;
    private readonly ILogger<EmailService> _logger = logger;

    /// <inheritdoc/>
    public async Task<PaginatedResponseDto<EmailSummaryDto>> ListAsync(Guid teamId, ListEmailsActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var emails = _ctx.Emails.AsNoTracking().Where(e => e.TeamId == teamId);
        var total = await emails.CountAsync(cancellationToken);
        var items = await emails
            .OrderByDescending(e => e.DateModified ?? e.DateCreated)
            .ThenBy(e => e.Id)
            .Skip((action.Page - 1) * action.PageSize)
            .Take(action.PageSize)
            .Select(EmailMapper.MapToEmailSummaryDtoExpression)
            .ToListAsync(cancellationToken);

        return new PaginatedResponseDto<EmailSummaryDto>(items, action.Page, action.PageSize, total);
    }

    /// <inheritdoc/>
    public async Task<EmailDto> GetAsync(Guid teamId, Guid id, CancellationToken cancellationToken)
    {
        var email = await _ctx.Emails
            .AsNoTracking()
            .Where(e => e.Id == id && e.TeamId == teamId)
            .Select(EmailMapper.MapToEmailDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);

        return email ?? throw new NotFoundException(NotFoundMessage);
    }

    /// <inheritdoc/>
    public async Task<EmailDto> CreateAsync(Guid teamId, Guid authorId, CreateEmailActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var email = new Email
        {
            TeamId = teamId,
            AuthorId = authorId,
            Subject = action.Subject.Trim(),
            BodyHtml = EmailBodySanitizer.Sanitize(action.BodyHtml),
            StatusId = EmailStatuses.Draft,
        };
        await ApplyRecipientsAsync(email, action.Recipients, cancellationToken);
        _ctx.Add(email);
        await SaveAsync(cancellationToken);

        return EmailMapper.MapToEmailDto(email);
    }

    /// <inheritdoc/>
    public async Task<EmailDto> CreateFromTemplateAsync(Guid teamId, Guid authorId, CreateEmailFromTemplateActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var template = await _ctx.EmailTemplates
            .AsNoTracking()
            .Include(t => t.Recipients)
            .Include(t => t.Attachments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == action.TemplateId && t.TeamId == teamId, cancellationToken)
            ?? throw new NotFoundException(TemplateNotFoundMessage);
        if (template.Attachments.Any(a => a.StatusId is AttachmentStatuses.Pending or AttachmentStatuses.Uploading))
        {
            throw new TemplateNotReadyException();
        }

        // The id up front, so the copied documents can be named under the email before it is saved.
        var email = new Email
        {
            Id = Guid.CreateVersion7(),
            TeamId = teamId,
            AuthorId = authorId,
            TemplateId = template.Id,

            // Stored sanitized and trimmed on the template already.
            Subject = template.Subject,
            BodyHtml = template.BodyHtml,
            StatusId = EmailStatuses.Draft,
        };

        // Through the email path, so the directory names the recipients as it does for any draft.
        EmailRecipientActionDto[] recipients =
        [
            .. template.Recipients
                .OrderBy(r => r.RecipientTypeId)
                .ThenBy(r => r.Position)
                .Select(r => new EmailRecipientActionDto(r.Address, r.RecipientTypeId)),
        ];
        await ApplyRecipientsAsync(email, recipients, cancellationToken);

        await CopyThenSaveAsync(email, template.Attachments.Select(a => ((IStagedAttachment)a, a.FileName, a.DocumentTypeId)), "One of the template's documents is no longer in storage. Remove it from the template and try again.", cancellationToken);

        return EmailMapper.MapToEmailDto(email);
    }

    /// <inheritdoc/>
    public async Task<EmailDto> DuplicateAsync(Guid teamId, Guid id, Guid authorId, CancellationToken cancellationToken)
    {
        var source = await _ctx.Emails
            .AsNoTracking()
            .Include(e => e.Recipients)
            .Include(e => e.Attachments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id && e.TeamId == teamId, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);
        if (source.Attachments.Any(a => a.StatusId is AttachmentStatuses.Pending or AttachmentStatuses.Uploading))
        {
            throw new EmailNotReadyException();
        }

        // The id up front, so the copied documents can be named under the email before it is saved.
        var email = new Email
        {
            Id = Guid.CreateVersion7(),
            TeamId = teamId,
            AuthorId = authorId,
            TemplateId = source.TemplateId,

            // Stored sanitized and trimmed on the source already.
            Subject = source.Subject,
            BodyHtml = source.BodyHtml,
            StatusId = EmailStatuses.Draft,
        };

        // As stored, directory names included: the source resolved them, and a copy needs no second lookup.
        foreach (var recipient in source.Recipients.OrderBy(r => r.RecipientTypeId).ThenBy(r => r.Position))
        {
            email.Recipients.Add(new EmailRecipient
            {
                Address = recipient.Address,
                RecipientTypeId = recipient.RecipientTypeId,
                Position = recipient.Position,
                UserId = recipient.UserId,
                DisplayName = recipient.DisplayName,
            });
        }

        // Only a draft's attachments can be removed, so a sent email gets no advice it could not follow.
        var missingDocumentMessage = source.StatusId == EmailStatuses.Draft
            ? "One of the email's documents is no longer in storage. Remove it from the email and try again."
            : "One of the email's documents is no longer in storage, so the email cannot be duplicated.";
        await CopyThenSaveAsync(email, source.Attachments.Select(a => ((IStagedAttachment)a, a.FileName, a.DocumentTypeId)), missingDocumentMessage, cancellationToken);

        return EmailMapper.MapToEmailDto(email);
    }

    /// <inheritdoc/>
    public async Task<EmailDto> UpdateAsync(Guid teamId, Guid id, UpdateEmailActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var email = await _ctx.Emails
            .Include(e => e.Recipients)
            .Include(e => e.Attachments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id && e.TeamId == teamId, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);
        if (email.StatusId != EmailStatuses.Draft)
        {
            throw new EmailNotEditableException();
        }

        // The version the client last read, so the UPDATE matches no row if anyone changed the email since.
        _ctx.Entry(email).Property(e => e.Version).OriginalValue = Convert.FromBase64String(action.Version);
        email.Subject = action.Subject.Trim();
        email.BodyHtml = EmailBodySanitizer.Sanitize(action.BodyHtml);
        await ApplyRecipientsAsync(email, action.Recipients, cancellationToken);

        // A recipients-only change touches no column of the email itself; marking it modified keeps the version check and bumps the version.
        _ctx.Entry(email).State = EntityState.Modified;
        await SaveAsync(cancellationToken);

        return EmailMapper.MapToEmailDto(email);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid teamId, Guid id, CancellationToken cancellationToken)
    {
        var email = await _ctx.Emails.FirstOrDefaultAsync(e => e.Id == id && e.TeamId == teamId, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);

        // DeactivationInterceptor turns the delete into a DateDeactivated stamp; the recipient and attachment filters hide the children with it.
        _ctx.Remove(email);
        await SaveAsync(cancellationToken);
    }

    #region Private methods

    // Keeps the rows of addresses that stay, so their directory lookup is not repeated and unchanged recipients are not rewritten.
    private async Task ApplyRecipientsAsync(Email email, IReadOnlyList<EmailRecipientActionDto> recipients, CancellationToken cancellationToken)
    {
        var wanted = recipients
            .Select((r, position) => (Address: r.Address.Trim().ToLowerInvariant(), r.Type, Position: position))
            .ToList();
        var existing = email.Recipients.ToDictionary(r => r.Address, StringComparer.Ordinal);
        var addresses = wanted.Select(w => w.Address).ToHashSet(StringComparer.Ordinal);
        foreach (var stale in existing.Values.Where(r => !addresses.Contains(r.Address)).ToList())
        {
            email.Recipients.Remove(stale);
            _ctx.Remove(stale);
        }

        var newAddresses = wanted.Where(w => !existing.ContainsKey(w.Address)).Select(w => w.Address).ToList();
        var accounts = await _directory.FindByEmailsAsync(newAddresses, cancellationToken);
        foreach (var (address, type, position) in wanted)
        {
            if (existing.TryGetValue(address, out var recipient))
            {
                recipient.RecipientTypeId = type;
                recipient.Position = position;
                continue;
            }

            var account = accounts.GetValueOrDefault(address);
            email.Recipients.Add(new EmailRecipient
            {
                Address = address,
                RecipientTypeId = type,
                Position = position,
                UserId = account?.Id,
                DisplayName = account is null ? null : DisplayNameOf(account),
            });
        }
    }

    private static string DisplayNameOf(DirectoryUser account)
    {
        var name = User.ComposeFullName(account.FirstName, account.LastName);

        return name.Length <= EmailRecipientLimits.DisplayNameMaxLength ? name : name[..EmailRecipientLimits.DisplayNameMaxLength];
    }

    // Only uploaded documents: a failed one has nothing in storage to copy. The id is set here so the blob can be named after it.
    private async Task CopyThenSaveAsync(
        Email email,
        IEnumerable<(IStagedAttachment Stored, string FileName, DocumentTypes DocumentTypeId)> sources,
        string missingDocumentMessage,
        CancellationToken cancellationToken)
    {
        List<(string Source, string Destination)> blobs = [];
        foreach (var (stored, fileName, documentTypeId) in sources.Where(s => s.Stored.StatusId == AttachmentStatuses.Uploaded).OrderBy(s => s.Stored.DateCreated).ThenBy(s => s.Stored.Id))
        {
            var id = Guid.CreateVersion7();
            var attachment = new EmailAttachment
            {
                Id = id,
                EmailId = email.Id,
                FileName = fileName,
                DocumentTypeId = documentTypeId,
                ContentType = stored.ContentType,
                SizeBytes = stored.SizeBytes,
                BlobName = $"{email.Id:N}/{id:N}",
                StatusId = AttachmentStatuses.Uploaded,
            };
            email.Attachments.Add(attachment);
            blobs.Add((stored.BlobName, attachment.BlobName));
        }

        await AttachmentCopy.CopyThenSaveAsync(
            _store,
            _logger,
            email.Id,
            blobs,
            async ct =>
            {
                _ctx.Add(email);
                await SaveAsync(ct);
            },
            ct => _ctx.Emails.AsNoTracking().IgnoreQueryFilters(["Deactivation", "TeamDeactivation"]).AnyAsync(e => e.Id == email.Id, ct),
            missingDocumentMessage,
            cancellationToken);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _ctx.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The email was changed by another request. Read it again and retry.");
        }
    }

    #endregion
}
