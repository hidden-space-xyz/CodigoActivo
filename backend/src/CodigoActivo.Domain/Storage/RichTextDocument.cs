using System.Text.Json;

namespace CodigoActivo.Domain.Storage;

public static class RichTextDocument
{
    public static bool IsEmpty(string? richTextJson)
    {
        if (string.IsNullOrWhiteSpace(richTextJson))
            return true;

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
        return element.ValueKind switch
        {
            JsonValueKind.Object => ObjectHasContent(element),
            JsonValueKind.Array => element.EnumerateArray().Any(HasContent),
            _ => false,
        };
    }

    private static bool ObjectHasContent(JsonElement element)
    {
        if (
            element.TryGetProperty("text", out var text)
            && text.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(text.GetString())
        )
            return true;

        if (
            element.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String
            && type.GetString() == "image"
        )
            return true;

        return element.EnumerateObject().Any(property => HasContent(property.Value));
    }
}
