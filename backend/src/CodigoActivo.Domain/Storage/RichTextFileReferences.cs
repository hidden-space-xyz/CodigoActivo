using System.Text.RegularExpressions;

namespace CodigoActivo.Domain.Storage;

public static partial class RichTextFileReferences
{
    public static string ContentUrlMarker(Guid fileId)
    {
        return $"/api/files/{fileId}/content";
    }

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
