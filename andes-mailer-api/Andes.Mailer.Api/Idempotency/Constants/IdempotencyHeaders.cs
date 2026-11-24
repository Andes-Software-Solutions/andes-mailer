namespace Andes.Mailer.Api.Idempotency.Constants;

internal static class IdempotencyHeaders
{
    public const string Key = "Idempotency-Key";
    public const string Replayed = "Idempotent-Replayed";
    public const int KeyMaxLength = 128;
}
