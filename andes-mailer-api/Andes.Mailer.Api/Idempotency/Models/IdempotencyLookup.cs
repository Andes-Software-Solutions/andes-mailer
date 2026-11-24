using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Api.Idempotency.Models;

/// <summary>
/// What the idempotency store found for a key.
/// </summary>
/// <param name="Outcome">Whether the request runs, waits, is rejected, or is replayed.</param>
/// <param name="Result">The stored endpoint result when <paramref name="Outcome"/> is <see cref="IdempotencyOutcomes.Completed"/>; otherwise <see langword="null"/>.</param>
internal readonly record struct IdempotencyLookup(IdempotencyOutcomes Outcome, object? Result = null);
