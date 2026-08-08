using System.Globalization;
using System.Text;
using System.Xml.Linq;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Seo.Queries;

public sealed record GetSitemapXmlQuery : IQuery<string>;

public sealed class GetSitemapXmlQueryHandler(
    IEventRepository events,
    IAnnouncementRepository announcements,
    IResourceRepository resources,
    IQueryExecutor executor,
    ApplicationOptions application,
    HybridCache cache
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

    public async Task<string> HandleAsync(GetSitemapXmlQuery query, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "sitemap",
            token => new ValueTask<string>(BuildSitemapXmlAsync(token)),
            CachePolicies.PublicContent,
            [CacheTags.Events, CacheTags.Announcements, CacheTags.Resources],
            ct
        );
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
        public override Encoding Encoding => Encoding.UTF8;
    }
}
