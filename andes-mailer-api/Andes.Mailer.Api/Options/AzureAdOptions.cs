using FluentValidation;

namespace Andes.Mailer.Api.Options;

/// <summary>
/// Holds the Microsoft Entra ID application whose access tokens this API accepts.
/// </summary>
public sealed class AzureAdOptions
{
    /// <summary>
    /// The configuration section these options bind to.
    /// </summary>
    public const string SectionName = "AzureAd";

    /// <summary>
    /// Gets the Entra sign-in endpoint.
    /// </summary>
    public string Instance { get; init; } = string.Empty;

    /// <summary>
    /// Gets the directory (tenant) id that issues the tokens.
    /// </summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the application (client) id, which is also the token audience.
    /// </summary>
    public string ClientId { get; init; } = string.Empty;
}

internal sealed class AzureAdOptionsValidator : AbstractValidator<AzureAdOptions>
{
    public AzureAdOptionsValidator()
    {
        RuleFor(o => o.Instance).Must(IsHttpsUrl).WithMessage("Must be an absolute https URL.");
        RuleFor(o => o.TenantId).Must(IsGuid).WithMessage("Must be a GUID.");
        RuleFor(o => o.ClientId).Must(IsGuid).WithMessage("Must be a GUID.");
    }

    #region Private methods

    private static bool IsGuid(string value) => Guid.TryParse(value, out _);

    private static bool IsHttpsUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    #endregion
}
