using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Dto.Emails;

/// <summary>
/// Describes the delivery of an email to one of its recipients.
/// </summary>
/// <param name="Id">The delivery's id.</param>
/// <param name="Address">The recipient's address.</param>
/// <param name="DisplayName">The recipient's directory display name, or <see langword="null"/> for an address outside the directory.</param>
/// <param name="Status">One of the enumeration values that specifies the stage of the delivery.</param>
/// <param name="AttemptCount">The number of attempts made so far.</param>
/// <param name="NextAttemptAt">When the delivery is next due, or <see langword="null"/> once it is sent or has failed.</param>
/// <param name="DateSent">When the mail server accepted the message, or <see langword="null"/> if it has not.</param>
/// <param name="LastError">Why the last attempt failed, or <see langword="null"/> if no attempt failed.</param>
public sealed record EmailDeliveryDto(
    Guid Id,
    string Address,
    string? DisplayName,
    DeliveryStatuses Status,
    int AttemptCount,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? DateSent,
    string? LastError);
