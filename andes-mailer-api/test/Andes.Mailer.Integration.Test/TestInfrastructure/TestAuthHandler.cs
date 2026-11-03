using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Andes.Mailer.Integration.Test.TestInfrastructure;

/// <summary>
/// Stands in for a validated Entra token, so tests cover the authorization rules without a live tenant.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string ScopesHeader = "X-Test-Scopes";
    public const string UserId = "7d6f0a2e-1b3c-4d5e-8f90-a1b2c3d4e5f6";
    public const string UserName = "Ada Lovelace";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ScopesHeader, out var scopes))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        Claim[] claims = [new("scp", scopes.ToString()), new("oid", UserId), new("name", UserName)];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
