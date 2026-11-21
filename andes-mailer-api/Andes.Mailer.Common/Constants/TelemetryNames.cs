namespace Andes.Mailer.Common.Constants;

/// <summary>
/// Names the service, activities, tags, and log properties this solution writes to telemetry, so the API, the background services, and
/// the queries in <c>docs/telemetry</c> agree on them.
/// </summary>
public static class TelemetryNames
{
    /// <summary>
    /// The OpenTelemetry <c>service.name</c> of the API, which Application Insights shows as <c>cloud_RoleName</c>.
    /// </summary>
    public const string ServiceName = "andes-mailer-api";

    /// <summary>
    /// The activity source the background services start their activities from.
    /// </summary>
    public const string ActivitySourceName = "Andes.Mailer";

    /// <summary>
    /// The activity for one run of the email dispatcher.
    /// </summary>
    public const string EmailDispatchActivity = "email.dispatch";

    /// <summary>
    /// The activity for one attempt to send one delivery.
    /// </summary>
    public const string EmailDeliveryActivity = "email.deliver";

    /// <summary>
    /// The activity for one attempt to upload one staged attachment.
    /// </summary>
    public const string AttachmentUploadActivity = "attachment.upload";

    /// <summary>
    /// The OpenTelemetry semantic-convention tag for the caller, which Application Insights maps to <c>user_AuthenticatedId</c>. It holds
    /// the Entra object id and nothing else.
    /// </summary>
    public const string EndUserIdTag = "enduser.id";

    /// <summary>
    /// The tag for the team a request is routed to.
    /// </summary>
    public const string TeamIdTag = "andes.team_id";

    /// <summary>
    /// The tag for the delivery an <see cref="EmailDeliveryActivity"/> sends.
    /// </summary>
    public const string DeliveryIdTag = "andes.delivery_id";

    /// <summary>
    /// The tag for the attachment an <see cref="AttachmentUploadActivity"/> uploads.
    /// </summary>
    public const string AttachmentIdTag = "andes.attachment_id";

    /// <summary>
    /// The tag for what owns the attachment an <see cref="AttachmentUploadActivity"/> uploads: an email or a template.
    /// </summary>
    public const string AttachmentOwnerTag = "andes.attachment_owner";

    /// <summary>
    /// The log scope property for the caller's Entra object id, which Application Insights shows in <c>customDimensions</c>.
    /// </summary>
    public const string UserIdProperty = "UserId";

    /// <summary>
    /// The log scope property for the team a request is routed to.
    /// </summary>
    public const string TeamIdProperty = "TeamId";
}
