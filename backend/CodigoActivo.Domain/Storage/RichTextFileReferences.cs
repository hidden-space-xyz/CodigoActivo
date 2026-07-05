using System.Text.RegularExpressions;

namespace CodigoActivo.Domain.Storage;

/// <summary>
/// Rich-text descriptions (TipTap JSON on events, announcements and resources) embed uploaded
/// images as plain "/api/files/{id}/content" URLs with no FK backing them. This is the single
/// place that knows that stored format — both for extracting the referenced file ids from a
/// document and for building the text marker the in-use check scans descriptions for.
/// </summary>
public static partial class RichTextFileReferences
{
    /// <summary>The substring a description contains iff it embeds the given file.</summary>
    public static string ContentUrlMarker(Guid fileId)
    {
        return $"/api/files/{fileId}/content";
    }

    /// <summary>File ids embedded as content URLs in the document (absolute or relative).</summary>
    public static HashSet<Guid> Extract(string? richTextJson)
    {
        var ids = new HashSet<Guid>();
        if (string.IsNullOrEmpty(richTextJson)) return ids;

        foreach (Match match in ContentUrl().Matches(richTextJson))
        {
            if (Guid.TryParse(match.Groups[1].Value, out var id)) ids.Add(id);
        }

        return ids;
    }

    /// <summary>File ids embedded in the previous document but no longer in the current one.</summary>
    public static HashSet<Guid> ExtractRemoved(string? previous, string? current)
    {
        var removed = Extract(previous);
        removed.ExceptWith(Extract(current));
        return removed;
    }

    [GeneratedRegex(
        "/api/files/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/content"
    )]
    private static partial Regex ContentUrl();
}
