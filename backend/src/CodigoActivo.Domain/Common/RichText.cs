using System.Text.Json;

namespace CodigoActivo.Domain.Common;

/// <summary>
/// Rich-text document written in the editor and stored as its JSON. It has content when some
/// node holds non-blank text or is an image.
/// </summary>
public sealed record RichText
{
    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowDuplicateProperties = false,
    };

    private RichText(string json)
    {
        Json = json;
    }

    /// <summary>
    /// Gets the document without content.
    /// </summary>
    public static RichText Empty { get; } = new("{}");

    /// <summary>
    /// Gets the JSON of the document.
    /// </summary>
    public string Json { get; }

    /// <summary>
    /// Gets a value indicating whether the document has no text and no image, or is not a JSON
    /// document.
    /// </summary>
    public bool IsEmpty => !HasContent(Json);

    /// <summary>
    /// Wraps the JSON of a document.
    /// </summary>
    /// <param name="json">JSON of the document.</param>
    /// <returns>The document.</returns>
    public static RichText From(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return new RichText(json);
    }

    /// <summary>
    /// Wraps the JSON of a document that may be missing.
    /// </summary>
    /// <param name="json">JSON of the document, or <see langword="null"/>.</param>
    /// <returns>The document, or <see cref="Empty"/> when there is none.</returns>
    public static RichText FromOptional(string? json)
    {
        return json is null ? Empty : new RichText(json);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Json;
    }

    private static bool HasContent(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json, ParseOptions);
            return HasContent(document.RootElement);
        }
        catch (JsonException)
        {
            return false;
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
