using System.Text.Json;

namespace CodigoActivo.Domain.Storage;

/// <summary>
/// Parses and validates rich text documents.
/// </summary>
public static class RichTextDocument
{
    /// <summary>
    /// Determines whether the rich-text document contains meaningful content.
    /// </summary>
    /// <param name="richTextJson">Rich-text document encoded as JSON.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool IsEmpty(string? richTextJson)
    {
        if (string.IsNullOrWhiteSpace(richTextJson))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(richTextJson);
            return !HasContent(document.RootElement);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static bool HasContent(JsonElement element)
    {
        return element.ValueKind is JsonValueKind.Object
            ? ObjectHasContent(element)
            : element.ValueKind is JsonValueKind.Array && element.EnumerateArray().Any(HasContent);
    }

    private static bool ObjectHasContent(JsonElement element)
    {
        if (
            element.TryGetProperty("text", out var text)
            && text.ValueKind is JsonValueKind.String
            && !string.IsNullOrWhiteSpace(text.GetString())
        )
        {
            return true;
        }

        var isImage =
            element.TryGetProperty("type", out var type)
            && type.ValueKind is JsonValueKind.String
            && string.Equals(type.GetString(), "image", StringComparison.Ordinal);

        return isImage || element.EnumerateObject().Any(property => HasContent(property.Value));
    }
}
