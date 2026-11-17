using Andes.Mailer.Entity.Base;
using Andes.Mailer.Entity.Teams;
using Andes.Mailer.Entity.Users;

namespace Andes.Mailer.Entity.Emails.Templates;

/// <summary>
/// Represents a reusable starting point for a team's emails, with the recipients and attachments every email made from it begins with.
/// </summary>
/// <remarks>
/// Only the team's administrators change a template; every member of the team can start an email from it. An email copies the template
/// when it is created, so later changes to the template never reach emails already made from it.
/// </remarks>
public sealed class EmailTemplate : BaseModifiedEntity<Guid>
{
    /// <summary>
    /// Gets or sets the id of the team the template belongs to.
    /// </summary>
    public Guid TeamId { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public Team? Team { get; set; }

    /// <summary>
    /// Gets or sets the Entra object id of the user who created the template.
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Gets or sets the user who created the template.
    /// </summary>
    public User? Author { get; set; }

    /// <summary>
    /// Gets or sets the name the team picks the template by.
    /// </summary>
    /// <value>Unique among the team's active templates.</value>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the body.
    /// </summary>
    /// <value>Sanitized HTML, safe to render as stored.</value>
    public string BodyHtml { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recipients.
    /// </summary>
    public ICollection<EmailTemplateRecipient> Recipients { get; set; } = [];

    /// <summary>
    /// Gets or sets the attachments.
    /// </summary>
    public ICollection<EmailTemplateAttachment> Attachments { get; set; } = [];
}
