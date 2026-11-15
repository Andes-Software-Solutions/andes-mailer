using System.Text.Json.Serialization;

namespace Andes.Mailer.Api.Configuration;

internal static class JsonConfiguration
{
    #region Public static methods

    // Enums go over the wire by name ("Pending", "Cc"), so clients never depend on the stored numbers and OpenAPI lists the values.
    public static IServiceCollection AddAndesMailerJson(this IServiceCollection services) =>
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    #endregion
}
