namespace Andes.Mailer.Dto.Emails;

/// <summary>
/// Counts an email's deliveries by outcome.
/// </summary>
/// <param name="Pending">The number of recipients still waiting for, or in the middle of, an attempt.</param>
/// <param name="Sent">The number of recipients the mail server accepted the message for.</param>
/// <param name="Failed">The number of recipients every attempt failed for.</param>
public sealed record EmailDeliverySummaryDto(int Pending, int Sent, int Failed);
