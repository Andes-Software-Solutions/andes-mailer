using System.Linq.Expressions;

using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Entity.Emails;

namespace Andes.Mailer.Service.Emails;

/// <summary>
/// Maps emails, their recipients, and their attachments to response DTOs.
/// </summary>
public static class EmailMapper
{
    /// <summary>
    /// Gets the projection that maps an email to an <see cref="EmailDto"/> inside a query.
    /// </summary>
    /// <remarks>
    /// The orderings match <see cref="MapToEmailDto"/>, so an email reads back in the order it was saved whichever path built the DTO.
    /// </remarks>
    public static Expression<Func<Email, EmailDto>> MapToEmailDtoExpression { get; } = email => new EmailDto(
        email.Id,
        email.AuthorId,
        email.TemplateId,
        email.Subject,
        email.BodyHtml,
        email.StatusId,
        email.Recipients
            .OrderBy(r => r.RecipientTypeId)
            .ThenBy(r => r.Position)
            .Select(r => new EmailRecipientDto(r.Address, r.RecipientTypeId, r.UserId, r.DisplayName))
            .ToList(),
        email.Attachments
            .OrderBy(a => a.DateCreated)
            .ThenBy(a => a.Id)
            .Select(a => new EmailAttachmentDto(a.Id, a.FileName, a.DocumentTypeId, a.ContentType, a.SizeBytes, a.StatusId, a.FailureReason, a.DateCreated))
            .ToList(),
        email.DateCreated,
        email.DateModified,
        Convert.ToBase64String(email.Version),
        email.ScheduledFor,
        email.SentById,
        email.DateSendRequested,
        email.DateCompleted,
        new EmailDeliverySummaryDto(
            email.Deliveries.Count(d => d.StatusId == DeliveryStatuses.Pending || d.StatusId == DeliveryStatuses.Sending),
            email.Deliveries.Count(d => d.StatusId == DeliveryStatuses.Sent),
            email.Deliveries.Count(d => d.StatusId == DeliveryStatuses.Failed)));

    /// <summary>
    /// Gets the projection that maps an email to an <see cref="EmailSummaryDto"/> inside a query.
    /// </summary>
    public static Expression<Func<Email, EmailSummaryDto>> MapToEmailSummaryDtoExpression { get; } = email => new EmailSummaryDto(
        email.Id,
        email.AuthorId,
        email.TemplateId,
        email.Subject,
        email.StatusId,
        email.Recipients.Count,
        email.Attachments.Count,
        email.DateCreated,
        email.DateModified,
        email.ScheduledFor,
        email.DateCompleted,
        new EmailDeliverySummaryDto(
            email.Deliveries.Count(d => d.StatusId == DeliveryStatuses.Pending || d.StatusId == DeliveryStatuses.Sending),
            email.Deliveries.Count(d => d.StatusId == DeliveryStatuses.Sent),
            email.Deliveries.Count(d => d.StatusId == DeliveryStatuses.Failed)));

    /// <summary>
    /// Gets the projection that maps an attachment to an <see cref="EmailAttachmentDto"/> inside a query.
    /// </summary>
    public static Expression<Func<EmailAttachment, EmailAttachmentDto>> MapToEmailAttachmentDtoExpression { get; } = attachment => new EmailAttachmentDto(
        attachment.Id,
        attachment.FileName,
        attachment.DocumentTypeId,
        attachment.ContentType,
        attachment.SizeBytes,
        attachment.StatusId,
        attachment.FailureReason,
        attachment.DateCreated);

    #region Public static methods

    /// <summary>
    /// Maps a loaded email, with its recipients and attachments, to an <see cref="EmailDto"/>.
    /// </summary>
    /// <param name="email">The email, with <see cref="Email.Recipients"/> and <see cref="Email.Attachments"/> loaded.</param>
    /// <returns>The DTO.</returns>
    public static EmailDto MapToEmailDto(Email email)
    {
        ArgumentNullException.ThrowIfNull(email);

        return new EmailDto(
            email.Id,
            email.AuthorId,
            email.TemplateId,
            email.Subject,
            email.BodyHtml,
            email.StatusId,
            [.. email.Recipients
                .OrderBy(r => r.RecipientTypeId)
                .ThenBy(r => r.Position)
                .Select(r => new EmailRecipientDto(r.Address, r.RecipientTypeId, r.UserId, r.DisplayName))],
            [.. email.Attachments
                .Where(a => a.DateDeactivated is null)
                .OrderBy(a => a.DateCreated)
                .ThenBy(a => a.Id)
                .Select(MapToEmailAttachmentDto)],
            email.DateCreated,
            email.DateModified,
            Convert.ToBase64String(email.Version),
            email.ScheduledFor,
            email.SentById,
            email.DateSendRequested,
            email.DateCompleted,
            MapToEmailDeliverySummaryDto(email.Deliveries));
    }

    /// <summary>
    /// Counts loaded deliveries by outcome.
    /// </summary>
    /// <param name="deliveries">The deliveries; an email whose deliveries are not loaded counts as having none.</param>
    /// <returns>The DTO.</returns>
    public static EmailDeliverySummaryDto MapToEmailDeliverySummaryDto(IEnumerable<EmailDelivery> deliveries)
    {
        ArgumentNullException.ThrowIfNull(deliveries);

        var active = deliveries.Where(d => d.DateDeactivated is null).ToList();

        return new EmailDeliverySummaryDto(
            active.Count(d => d.StatusId is DeliveryStatuses.Pending or DeliveryStatuses.Sending),
            active.Count(d => d.StatusId == DeliveryStatuses.Sent),
            active.Count(d => d.StatusId == DeliveryStatuses.Failed));
    }

    /// <summary>
    /// Maps a loaded attachment to an <see cref="EmailAttachmentDto"/>.
    /// </summary>
    /// <param name="attachment">The attachment.</param>
    /// <returns>The DTO.</returns>
    public static EmailAttachmentDto MapToEmailAttachmentDto(EmailAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        return new EmailAttachmentDto(
            attachment.Id,
            attachment.FileName,
            attachment.DocumentTypeId,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.StatusId,
            attachment.FailureReason,
            attachment.DateCreated);
    }

    #endregion
}
