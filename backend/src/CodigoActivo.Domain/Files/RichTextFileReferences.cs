using System.Text.RegularExpressions;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Files;

/// <summary>
/// Defines how rich text references stored files and finds those references. Saved documents keep
/// each file's content URL and the API serves the file there, so the format cannot change without
/// rewriting them.
/// </summary>
public static partial class RichTextFileReferences
{
    /// <summary>
    /// Pattern of a file content URL embedded in rich text. Its only capture group is the file
    /// identifier, so the same text works as a .NET and as a PostgreSQL regular expression.
    /// </summary>
    public const string ContentUrlPattern =
        "/api/files/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/content";

    /// <summary>
    /// Builds the content URL a document uses to show a file.
    /// </summary>
    /// <param name="fileId">Identifier of the file.</param>
    /// <returns>The content URL of the file.</returns>
    public static string ContentUrl(StoredFileId fileId)
    {
        return $"/api/files/{fileId.Value}/content";
    }

    /// <summary>
    /// Tells whether a value is exactly the content URL of a file, with nothing before or after it.
    /// </summary>
    /// <param name="value">Value to check.</param>
    /// <returns><see langword="true"/> when the value is a file content URL.</returns>
    public static bool IsContentUrl(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return WholeContentUrl.IsMatch(value);
    }

    /// <summary>
    /// Returns the identifiers of the files a rich-text document shows.
    /// </summary>
    /// <param name="richText">Rich-text document.</param>
    /// <returns>The identifiers of the referenced files.</returns>
    public static IReadOnlySet<StoredFileId> Extract(RichText? richText)
    {
        if (richText is null)
        {
            return new HashSet<StoredFileId>();
        }

        return ContentUrls
            .Matches(richText.Json)
            .Select(match => StoredFileId.From(Guid.Parse(match.Groups[1].ValueSpan)))
            .ToHashSet();
    }

    /// <summary>
    /// Returns file identifiers present in the previous document but absent from the current one.
    /// </summary>
    /// <param name="previous">Previous rich-text document.</param>
    /// <param name="current">Current rich-text document.</param>
    /// <returns>The identifiers of the files no longer shown.</returns>
    public static IReadOnlySet<StoredFileId> ExtractRemoved(RichText? previous, RichText? current)
    {
        var removed = new HashSet<StoredFileId>(Extract(previous));
        removed.ExceptWith(Extract(current));
        return removed;
    }

    [GeneratedRegex(ContentUrlPattern, RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ContentUrls { get; }

    [GeneratedRegex(
        "^" + ContentUrlPattern + @"\z",
        RegexOptions.None,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex WholeContentUrl { get; }
}
