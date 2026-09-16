using System.Text.RegularExpressions;

namespace CodigoActivo.Domain.Storage;

/// <summary>
/// Extracts and compares file references in rich text file content.
/// </summary>
public static partial class RichTextFileReferences
{
    /// <summary>
    /// Builds the marker used to locate file URLs in rich-text JSON.
    /// </summary>
    /// <param name="fileId">Identifier of the file.</param>
    /// <returns>The generated text.</returns>
    public static string ContentUrlMarker(Guid fileId)
    {
        return $"/api/files/{fileId}/content";
    }

    /// <summary>
    /// Extracts every referenced file identifier from the rich-text document.
    /// </summary>
    /// <param name="richTextJson">Rich-text document encoded as JSON.</param>
    /// <returns>The resulting guid value.</returns>
    public static IReadOnlySet<Guid> Extract(string? richTextJson)
    {
        var ids = new HashSet<Guid>();
        if (string.IsNullOrEmpty(richTextJson))
        {
            return ids;
        }

        ids.UnionWith(
            ContentUrl
                .Matches(richTextJson)
                .Select(match =>
                    Guid.TryParse(match.Groups["id"].Value, out var id) ? id : (Guid?)null
                )
                .Where(id => id.HasValue)
                .Select(id => id.GetValueOrDefault())
        );

        return ids;
    }

    /// <summary>
    /// Returns file identifiers present in the previous document but absent from the current one.
    /// </summary>
    /// <param name="previous">Previous rich-text document.</param>
    /// <param name="current">Current rich-text document.</param>
    /// <returns>The resulting guid value.</returns>
    public static IReadOnlySet<Guid> ExtractRemoved(string? previous, string? current)
    {
        var removed = new HashSet<Guid>(Extract(previous));
        removed.ExceptWith(Extract(current));
        return removed;
    }

    [GeneratedRegex(
        "/api/files/(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/content",
        RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex ContentUrl { get; }
}
