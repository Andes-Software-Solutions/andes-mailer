namespace Andes.Mailer.Common.Enums;

/// <summary>
/// Specifies what a staged attachment belongs to, and so which table holds its row.
/// </summary>
public enum AttachmentOwners
{
    /// <summary>
    /// An email; the row is in <c>[Core].[EmailAttachment]</c>.
    /// </summary>
    Email = 1,

    /// <summary>
    /// An email template; the row is in <c>[Core].[EmailTemplateAttachment]</c>.
    /// </summary>
    Template = 2,
}
