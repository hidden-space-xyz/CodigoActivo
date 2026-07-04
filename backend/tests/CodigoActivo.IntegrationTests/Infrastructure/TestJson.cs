using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodigoActivo.IntegrationTests.Infrastructure;

/// <summary>
/// JSON options mirroring the API's serializer (camelCase, enums as strings) so requests are
/// serialized and responses deserialized exactly as the running app would.
/// </summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
