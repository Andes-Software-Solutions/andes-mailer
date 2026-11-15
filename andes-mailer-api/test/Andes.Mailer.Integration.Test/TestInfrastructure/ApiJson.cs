using System.Text.Json;
using System.Text.Json.Serialization;

namespace Andes.Mailer.Integration.Test.TestInfrastructure;

// Matches the API's wire format: web defaults plus enums by name.
public static class ApiJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
