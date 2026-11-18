using System.Globalization;
using System.Threading.RateLimiting;

using Andes.Mailer.Api.Options;
using Andes.Mailer.Common.Validation;

using FluentValidation;

using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace Andes.Mailer.Api.Configuration;

internal static class RateLimitingConfiguration
{
    public const string DirectorySearchPolicy = "directory-search";

    #region Public static methods

    public static IServiceCollection AddAndesMailerRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidator<DirectorySearchRateLimitOptions>, DirectorySearchRateLimitOptionsValidator>();
        services.AddOptions<DirectorySearchRateLimitOptions>()
            .Bind(configuration.GetSection(DirectorySearchRateLimitOptions.SectionName))
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        // No body here: UseStatusCodePages, earlier in the pipeline, writes the Problem Details for the 429.
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = static (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };

            // Per caller, so one busy user never throttles another; the policy runs after authorization, so the oid is always there.
            limiter.AddPolicy(DirectorySearchPolicy, static context =>
            {
                var options = context.RequestServices.GetRequiredService<IOptions<DirectorySearchRateLimitOptions>>().Value;
                var caller = context.User.GetObjectId() ?? context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

                return RateLimitPartition.GetTokenBucketLimiter(caller, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = options.TokenLimit,
                    TokensPerPeriod = options.TokensPerPeriod,
                    ReplenishmentPeriod = options.ReplenishmentPeriod,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
            });
        });

        return services;
    }

    #endregion
}
