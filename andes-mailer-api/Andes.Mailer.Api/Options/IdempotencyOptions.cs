using FluentValidation;

namespace Andes.Mailer.Api.Options;

/// <summary>
/// Holds how long the API remembers an <c>Idempotency-Key</c> and the response it produced.
/// </summary>
public sealed class IdempotencyOptions
{
    /// <summary>
    /// The configuration section these options bind to.
    /// </summary>
    public const string SectionName = "Idempotency";

    /// <summary>
    /// Gets how long a key is remembered after its request finished. The default is 5 minutes.
    /// </summary>
    public TimeSpan Expiration { get; init; } = TimeSpan.FromMinutes(5);
}

internal sealed class IdempotencyOptionsValidator : AbstractValidator<IdempotencyOptions>
{
    public IdempotencyOptionsValidator()
    {
        RuleFor(o => o.Expiration).InclusiveBetween(TimeSpan.FromSeconds(1), TimeSpan.FromHours(24));
    }
}
