using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Service.Emails.Attachments.Models;

/// <summary>
/// Identifies a staged attachment the background upload stores.
/// </summary>
/// <param name="Owner">One of the enumeration values that specifies what the attachment belongs to.</param>
/// <param name="Id">The attachment's id, which also names its staged file.</param>
public sealed record StagedAttachmentJob(AttachmentOwners Owner, Guid Id);
