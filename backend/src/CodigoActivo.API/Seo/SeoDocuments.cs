using System.Globalization;
using System.Text;
using System.Xml.Linq;
using CodigoActivo.Application.Seo;

namespace CodigoActivo.API.Seo;

/// <summary>
/// Renders the documents search engine crawlers read: <c>robots.txt</c> and the XML sitemap of the
/// single-page application routes.
/// </summary>
public static class SeoDocuments
{
    private static readonly XNamespace Xmlns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private static readonly string[] StaticPaths =
    [
        "/",
        "/about",
        "/events",
        "/news",
        "/resources",
        "/register",
    ];

    /// <summary>
    /// Renders the crawler rules, pointing at the sitemap of the public site.
    /// </summary>
    /// <param name="baseUrl">Public base URL of the site.</param>
    /// <returns>The <c>robots.txt</c> content.</returns>
    public static string RobotsTxt(string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        return string.Join(
            '\n',
            "User-agent: *",
            "Disallow: /admin",
            "Disallow: /api/",
            "Allow: /api/files/",
            "",
            $"Sitemap: {baseUrl.TrimEnd('/')}/sitemap.xml"
        );
    }

    /// <summary>
    /// Renders the sitemap with the static pages and every indexable content page.
    /// </summary>
    /// <param name="baseUrl">Public base URL of the site.</param>
    /// <param name="entries">Indexable content pages.</param>
    /// <returns>The sitemap XML document.</returns>
    public static string SitemapXml(string baseUrl, SitemapEntries entries)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(entries);
        var trimmed = baseUrl.TrimEnd('/');

        var urlSet = new XElement(Xmlns + "urlset");
        foreach (var path in StaticPaths)
        {
            urlSet.Add(new XElement(Xmlns + "url", new XElement(Xmlns + "loc", trimmed + path)));
        }

        AddEntityUrls(urlSet, trimmed, "events", entries.Events);
        AddEntityUrls(urlSet, trimmed, "news", entries.News);
        AddEntityUrls(urlSet, trimmed, "resources", entries.Resources);

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
            var lastModified = entry.LastModified.UtcDateTime.ToString(
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

    private sealed class Utf8StringWriter() : StringWriter(CultureInfo.InvariantCulture)
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
