namespace Andes.Mailer.Api.Problems.Constants;

internal static class ProblemTypes
{
    public const string BadRequest = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    public const string Forbidden = "https://tools.ietf.org/html/rfc9110#section-15.5.4";
    public const string NotFound = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    public const string Conflict = "https://tools.ietf.org/html/rfc9110#section-15.5.10";
    public const string Unexpected = "https://tools.ietf.org/html/rfc9110#section-15.6.1";
}
