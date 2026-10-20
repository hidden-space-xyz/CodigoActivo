using System.Globalization;
using System.Text;
using System.Text.Json;
using Serilog.Events;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Writes the values of a log event as JSON that a <c>jsonb</c> column accepts: texts are cleaned
/// by <see cref="LogText"/> and capped, numbers that JSON cannot represent (NaN and infinities)
/// become texts, and any other scalar is written as its invariant text.
/// </summary>
public static class LogJson
{
    /// <summary>
    /// Identifies the maximum length of each text value.
    /// </summary>
    public const int TextMaxLength = 4_000;

    /// <summary>
    /// Writes named values as one JSON object.
    /// </summary>
    /// <param name="properties">Values to write, in order.</param>
    /// <returns>The JSON object, or <see langword="null"/> when there is no value.</returns>
    public static string? FromProperties(IReadOnlyCollection<LogEventProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        if (properties.Count is 0)
        {
            return null;
        }

        return Write(writer =>
        {
            writer.WriteStartObject();
            foreach (var property in properties)
            {
                writer.WritePropertyName(LogText.Clean(property.Name, TextMaxLength));
                WriteValue(writer, property.Value);
            }

            writer.WriteEndObject();
        });
    }

    internal static string Write(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static void WriteValue(Utf8JsonWriter writer, LogEventPropertyValue value)
    {
        switch (value)
        {
            case ScalarValue scalar:
                WriteScalar(writer, scalar.Value);
                break;
            case SequenceValue sequence:
                writer.WriteStartArray();
                foreach (var element in sequence.Elements)
                {
                    WriteValue(writer, element);
                }

                writer.WriteEndArray();
                break;
            case StructureValue structure:
                writer.WriteStartObject();
                foreach (var property in structure.Properties)
                {
                    writer.WritePropertyName(LogText.Clean(property.Name, TextMaxLength));
                    WriteValue(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case DictionaryValue dictionary:
                writer.WriteStartObject();
                foreach (var (key, element) in dictionary.Elements)
                {
                    writer.WritePropertyName(Text(key.Value));
                    WriteValue(writer, element);
                }

                writer.WriteEndObject();
                break;
            default:
                writer.WriteStringValue(Text(value.ToString()));
                break;
        }
    }

    private static void WriteScalar(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case byte or sbyte or short or ushort or int or uint or long:
                writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case ulong number:
                writer.WriteNumberValue(number);
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case double number when double.IsFinite(number):
                writer.WriteNumberValue(number);
                break;
            case float number when float.IsFinite(number):
                writer.WriteNumberValue(number);
                break;
            case DateTimeOffset instant:
                writer.WriteStringValue(instant);
                break;
            case DateTime instant:
                writer.WriteStringValue(instant);
                break;
            default:
                writer.WriteStringValue(Text(value));
                break;
        }
    }

    private static string Text(object? value)
    {
        return LogText.Clean(
            Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            TextMaxLength
        );
    }
}
