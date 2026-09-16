using System.Globalization;
using System.Text;
using System.Xml.Linq;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Seo.Queries;

/// <summary>
/// Carries the criteria used to retrieve sitemap xml.
/// </summary>
public sealed record GetSitemapXmlQuery : IQuery<string>;

/// <summary>
/// Executes the query to retrieve sitemap xml.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="announcements">Repository used to persist and retrieve announcements.</param>
/// <param name="resources">Repository used to persist and retrieve resources.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="application">The application value.</param>
public sealed class GetSitemapXmlQueryHandler(
    IEventRepository events,
    IAnnouncementRepository announcements,
    IResourceRepository resources,
    IQueryExecutor executor,
    ApplicationOptions application
) : IQueryHandler<GetSitemapXmlQuery, string>
{
    private static readonly XNamespace Xmlns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private static readonly string[] StaticPaths =
    [
        "/",
        "/about",
        "/events",
        "/announcements",
        "/resources",
        "/register",
    ];

    /// <summary>
    /// Handles the request to retrieve sitemap xml.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a string.</returns>
    public Task<string> HandleAsync(GetSitemapXmlQuery query, CancellationToken ct = default)
    {
        return BuildSitemapXmlAsync(ct);
    }

    private async Task<string> BuildSitemapXmlAsync(CancellationToken ct)
    {
        var baseUrl = application.BaseUrl.TrimEnd('/');

        var eventEntries = await executor.ToListAsync(
            events.Query().Select(e => new SitemapEntry(e.Id, e.CreatedAt, e.UpdatedAt)),
            ct
        );
        var announcementEntries = await executor.ToListAsync(
            announcements.Query().Select(a => new SitemapEntry(a.Id, a.CreatedAt, a.UpdatedAt)),
            ct
        );
        var resourceEntries = await executor.ToListAsync(
            resources
                .Query()
                .Where(r => r.Url == null)
                .Select(r => new SitemapEntry(r.Id, r.CreatedAt, r.UpdatedAt)),
            ct
        );

        var urlSet = new XElement(Xmlns + "urlset");
        foreach (var path in StaticPaths)
        {
            urlSet.Add(new XElement(Xmlns + "url", new XElement(Xmlns + "loc", baseUrl + path)));
        }

        AddEntityUrls(urlSet, baseUrl, "events", eventEntries);
        AddEntityUrls(urlSet, baseUrl, "announcements", announcementEntries);
        AddEntityUrls(urlSet, baseUrl, "resources", resourceEntries);

        return Serialize(new XDocument(new XDeclaration("1.0", "utf-8", null), urlSet));
    }

    private static void AddEntityUrls(
        XElement urlSet,
        string baseUrl,
        string segment,
        IReadOnlyList<SitemapEntry> entries
    )
    {
        foreach (var entry in entries)
        {
            var lastModified = (entry.UpdatedAt ?? entry.CreatedAt).UtcDateTime.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture
            );
            urlSet.Add(
                new XElement(
                    Xmlns + "url",
                    new XElement(Xmlns + "loc", $"{baseUrl}/{segment}/{entry.Id}"),
                    new XElement(Xmlns + "lastmod", lastModified)
                )
            );
        }
    }

    private static string Serialize(XDocument document)
    {
        using var writer = new Utf8StringWriter();
        document.Save(writer);
        return writer.ToString();
    }

    private sealed record SitemapEntry(
        Guid Id,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt
    );

    private sealed class Utf8StringWriter() : StringWriter(CultureInfo.InvariantCulture)
    {
/// <inheritdoc />
        public override Encoding Encoding => Encoding.UTF8;
    }
}
