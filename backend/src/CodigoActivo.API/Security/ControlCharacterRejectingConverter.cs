using System.Text.Json;
using System.Text.Json.Serialization;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.API.Security;

/// <summary>
/// Reads every JSON string of a request body and refuses one holding a control character other than
/// a tab or a line break, so model binding answers it as invalid input. No text the application
/// accepts needs them, and PostgreSQL refuses U+0000, which would otherwise fail the request in the
/// database instead of being reported as a validation error.
/// </summary>
public sealed class ControlCharacterRejectingConverter : JsonConverter<string>
{
    /// <inheritdoc />
    public override string? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var value = reader.GetString();
        return value is not null && !PlainTextAttribute.IsPlain(value)
            ? throw new JsonException("The string holds a control character.")
            : value;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value);
    }
}
