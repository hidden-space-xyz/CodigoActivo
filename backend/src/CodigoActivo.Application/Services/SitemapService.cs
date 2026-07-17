using System.Globalization;
using System.Text;
using System.Xml.Linq;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Services;

public class SitemapService(
    IEventRepository events,
    IAnnouncementRepository announcements,
    IResourceRepository resources,
    IQueryExecutor executor,
    ApplicationOptions application,
    HybridCache cache
) : ISitemapService
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

    public async Task<string> GetSitemapXmlAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "sitemap",
            token => new ValueTask<string>(BuildSitemapXmlAsync(token)),
            CachePolicies.PublicContent,
            [CacheTags.Events, CacheTags.Announcements, CacheTags.Resources],
            ct
        );
    }

    public string GetRobotsTxt()
    {
        var baseUrl = application.BaseUrl.TrimEnd('/');
        return string.Join(
            '\n',
            "User-agent: *",
            "Disallow: /admin",
            "Disallow: /api/",
            "Allow: /api/files/",
            "",
            $"Sitemap: {baseUrl}/sitemap.xml"
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
            urlSet.Add(new XElement(Xmlns + "url", new XElement(Xmlns + "loc", baseUrl + path)));
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
