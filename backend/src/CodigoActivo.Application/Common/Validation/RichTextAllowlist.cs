using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodigoActivo.Application.Common.Validation;

/// <summary>
/// Checks rich-text JSON against the node, mark and attribute allowlists of the editor, the same
/// ones the client keeps when it parses a document. Links may only point to absolute HTTP(S) URLs
/// without credentials, <c>mailto:</c>, <c>tel:</c>, a root-relative path (never <c>//</c> or
/// <c>/\</c>, which browsers read as another host) or a fragment; images may only reference the
/// file content endpoint of this application; colors are hex or rgb values.
/// </summary>
public static partial class RichTextAllowlist
{
    /// <summary>
    /// Deepest nesting of nodes a document may have.
    /// </summary>
    public const int MaxDepth = 50;

    /// <summary>
    /// Largest number of nodes a document may have.
    /// </summary>
    public const int MaxNodes = 5_000;

    private const int MaxLabelLength = 300;

    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowDuplicateProperties = false,
        MaxDepth = (MaxDepth * 2) + 8,
    };

    private static readonly HashSet<string> NodeKeys = new(StringComparer.Ordinal)
    {
        "type",
        "attrs",
        "content",
        "text",
        "marks",
    };

    private static readonly HashSet<string> MarkKeys = new(StringComparer.Ordinal)
    {
        "type",
        "attrs",
    };

    private static readonly Dictionary<string, Dictionary<string, Func<JsonElement, bool>>> Nodes =
        new(StringComparer.Ordinal)
        {
            ["doc"] = Attributes(),
            ["paragraph"] = Attributes(("textAlign", IsAlignment)),
            ["text"] = Attributes(),
            ["heading"] = Attributes(
                ("level", value => IsInteger(value, 1, 6)),
                ("textAlign", IsAlignment)
            ),
            ["blockquote"] = Attributes(),
            ["bulletList"] = Attributes(),
            ["orderedList"] = Attributes(
                ("start", value => IsNull(value) || IsInteger(value, 1, 100_000)),
                ("type", value => IsNull(value) || IsMatch(value, ListType()))
            ),
            ["listItem"] = Attributes(),
            ["codeBlock"] = Attributes(
                ("language", value => IsNull(value) || IsMatch(value, CodeLanguage()))
            ),
            ["hardBreak"] = Attributes(),
            ["horizontalRule"] = Attributes(),
            ["image"] = Attributes(
                ("src", value => IsMatch(value, FileContentUrl())),
                ("alt", IsOptionalLabel),
                ("title", IsOptionalLabel),
                ("width", value => IsNull(value) || IsInteger(value, 1, 10_000)),
                ("height", value => IsNull(value) || IsInteger(value, 1, 10_000)),
                ("textAlign", IsAlignment)
            ),
            ["table"] = Attributes(),
            ["tableRow"] = Attributes(),
            ["tableHeader"] = CellAttributes(),
            ["tableCell"] = CellAttributes(),
        };

    private static readonly Dictionary<string, Dictionary<string, Func<JsonElement, bool>>> Marks =
        new(StringComparer.Ordinal)
        {
            ["bold"] = Attributes(),
            ["italic"] = Attributes(),
            ["strike"] = Attributes(),
            ["code"] = Attributes(),
            ["underline"] = Attributes(),
            ["link"] = Attributes(
                ("href", IsSafeLink),
                ("target", value => IsNull(value) || IsText(value, "_blank")),
                ("rel", value => IsNull(value) || IsMatch(value, LinkRel())),
                ("class", value => IsNull(value) || IsMatch(value, CssClasses())),
                ("title", IsOptionalLabel)
            ),
            ["textStyle"] = Attributes(("color", IsOptionalColor)),
            ["highlight"] = Attributes(("color", IsOptionalColor)),
        };

    /// <summary>
    /// Tells whether a JSON text is an allowed rich-text document. An empty JSON object counts as an
    /// empty document.
    /// </summary>
    /// <param name="json">Rich-text document encoded as JSON.</param>
    /// <param name="allowImages">Whether image nodes are allowed.</param>
    /// <returns><see langword="true"/> when the document only uses allowed content.</returns>
    public static bool IsAllowed(string json, bool allowImages)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using var document = JsonDocument.Parse(json, ParseOptions);
            var root = document.RootElement;
            if (root.ValueKind is JsonValueKind.Object && !root.EnumerateObject().Any())
            {
                return true;
            }

            var nodes = 0;
            return IsText(TypeOf(root), "doc") && IsNode(root, allowImages, 0, ref nodes);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsNode(JsonElement node, bool allowImages, int depth, ref int nodes)
    {
        if (node.ValueKind is not JsonValueKind.Object || depth > MaxDepth || ++nodes > MaxNodes)
        {
            return false;
        }

        var type = TypeOf(node);
        if (
            type.ValueKind is not JsonValueKind.String
            || !Nodes.TryGetValue(type.GetString()!, out var attributes)
            || (!allowImages && IsText(type, "image"))
            || node.EnumerateObject().Any(property => !NodeKeys.Contains(property.Name))
            || !HasAllowedAttributes(node, attributes)
            || !HasAllowedMarks(node)
        )
        {
            return false;
        }

        var isText = IsText(type, "text");
        var hasText = node.TryGetProperty("text", out var text);
        if (isText != hasText || (hasText && text.ValueKind is not JsonValueKind.String))
        {
            return false;
        }

        if (!node.TryGetProperty("content", out var content))
        {
            return true;
        }

        if (isText || content.ValueKind is not JsonValueKind.Array)
        {
            return false;
        }

        foreach (var child in content.EnumerateArray())
        {
            if (!IsNode(child, allowImages, depth + 1, ref nodes))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasAllowedMarks(JsonElement node)
    {
        if (!node.TryGetProperty("marks", out var marks))
        {
            return true;
        }

        return marks.ValueKind is JsonValueKind.Array && marks.EnumerateArray().All(IsAllowedMark);
    }

    private static bool IsAllowedMark(JsonElement mark)
    {
        if (mark.ValueKind is not JsonValueKind.Object)
        {
            return false;
        }

        var type = TypeOf(mark);
        return type.ValueKind is JsonValueKind.String
            && Marks.TryGetValue(type.GetString()!, out var attributes)
            && mark.EnumerateObject().All(property => MarkKeys.Contains(property.Name))
            && HasAllowedAttributes(mark, attributes)
            && (!IsText(type, "link") || HasAttribute(mark, "href"));
    }

    private static bool HasAllowedAttributes(
        JsonElement element,
        Dictionary<string, Func<JsonElement, bool>> allowed
    )
    {
        var isImage = IsText(TypeOf(element), "image");
        if (!element.TryGetProperty("attrs", out var attrs))
        {
            return !isImage;
        }

        return attrs.ValueKind is JsonValueKind.Object
            && attrs
                .EnumerateObject()
                .All(attribute =>
                    allowed.TryGetValue(attribute.Name, out var isAllowed)
                    && isAllowed(attribute.Value)
                )
            && (!isImage || HasAttribute(element, "src"));
    }

    private static bool HasAttribute(JsonElement element, string name)
    {
        return element.TryGetProperty("attrs", out var attrs)
            && attrs.ValueKind is JsonValueKind.Object
            && attrs.TryGetProperty(name, out var value)
            && value.ValueKind is not JsonValueKind.Null;
    }

    private static JsonElement TypeOf(JsonElement element)
    {
        return
            element.ValueKind is JsonValueKind.Object
            && element.TryGetProperty("type", out var type)
            ? type
            : default;
    }

    private static bool IsSafeLink(JsonElement value)
    {
        if (value.ValueKind is not JsonValueKind.String)
        {
            return false;
        }

        var href = value.GetString()!.Trim();
        if (
            href.Length is 0
            || href.Any(char.IsControl)
            || href.StartsWith("//", StringComparison.Ordinal)
            || href.StartsWith("/\\", StringComparison.Ordinal)
        )
        {
            return false;
        }

        if (href.StartsWith('/') || href.StartsWith('#'))
        {
            return true;
        }

        if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        {
            return !string.IsNullOrWhiteSpace(uri.IdnHost) && string.IsNullOrEmpty(uri.UserInfo);
        }

        return uri.Scheme == Uri.UriSchemeMailto
            || string.Equals(uri.Scheme, "tel", StringComparison.Ordinal);
    }

    private static bool IsAlignment(JsonElement value)
    {
        return IsNull(value)
            || IsText(value, "left")
            || IsText(value, "center")
            || IsText(value, "right")
            || IsText(value, "justify");
    }

    private static bool IsOptionalColor(JsonElement value)
    {
        return IsNull(value) || IsMatch(value, Color());
    }

    private static bool IsOptionalLabel(JsonElement value)
    {
        return IsNull(value)
            || (
                value.ValueKind is JsonValueKind.String
                && value.GetString()!.Length <= MaxLabelLength
            );
    }

    private static bool IsColumnWidths(JsonElement value)
    {
        return IsNull(value)
            || (
                value.ValueKind is JsonValueKind.Array
                && value.GetArrayLength() <= 100
                && value.EnumerateArray().All(width => IsInteger(width, 1, 2_000))
            );
    }

    private static bool IsNull(JsonElement value)
    {
        return value.ValueKind is JsonValueKind.Null;
    }

    private static bool IsText(JsonElement value, string expected)
    {
        return value.ValueKind is JsonValueKind.String
            && string.Equals(value.GetString(), expected, StringComparison.Ordinal);
    }

    private static bool IsMatch(JsonElement value, Regex pattern)
    {
        return value.ValueKind is JsonValueKind.String && pattern.IsMatch(value.GetString()!);
    }

    private static bool IsInteger(JsonElement value, int minimum, int maximum)
    {
        return value.ValueKind is JsonValueKind.Number
            && value.TryGetInt32(out var number)
            && number >= minimum
            && number <= maximum;
    }

    private static Dictionary<string, Func<JsonElement, bool>> Attributes(
        params (string Name, Func<JsonElement, bool> IsAllowed)[] attributes
    )
    {
        return attributes.ToDictionary(
            attribute => attribute.Name,
            attribute => attribute.IsAllowed,
            StringComparer.Ordinal
        );
    }

    private static Dictionary<string, Func<JsonElement, bool>> CellAttributes()
    {
        return Attributes(
            ("colspan", value => IsNull(value) || IsInteger(value, 1, 100)),
            ("rowspan", value => IsNull(value) || IsInteger(value, 1, 100)),
            ("colwidth", IsColumnWidths)
        );
    }

    [GeneratedRegex(
        @"^/api/files/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/content\z",
        RegexOptions.None,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex FileContentUrl();

    [GeneratedRegex(
        @"^(#[0-9a-fA-F]{3}|#[0-9a-fA-F]{6}|rgba?\(\s*\d{1,3}%?\s*(,\s*\d{1,3}%?\s*){2}(,\s*(0|1|0?\.\d+)\s*)?\))\z",
        RegexOptions.None,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex Color();

    [GeneratedRegex(@"^[a-zA-Z0-9_+.-]{1,40}\z", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CodeLanguage();

    [GeneratedRegex(@"^[1aAiI]\z", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ListType();

    [GeneratedRegex(@"^[a-z ]{0,100}\z", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LinkRel();

    [GeneratedRegex(@"^[\w -]{0,100}\z", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CssClasses();
}
