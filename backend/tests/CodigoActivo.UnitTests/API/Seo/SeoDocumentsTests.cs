using AwesomeAssertions;
using CodigoActivo.API.Seo;
using CodigoActivo.Application.Seo;
using Xunit;

namespace CodigoActivo.UnitTests.API.Seo;

public sealed class SeoDocumentsTests
{
    private const string BaseUrl = "https://codigoactivo.test";

    private static readonly SitemapEntries NoEntries = new([], [], []);

    [Fact]
    public void RobotsTxtTrailingSlashBaseUrlReturnsExactRulesWithTrimmedBase()
    {
        var robots = SeoDocuments.RobotsTxt(BaseUrl + "/");

        var expected = string.Join(
            '\n',
            "User-agent: *",
            "Disallow: /admin",
            "Disallow: /api/",
            "Allow: /api/files/",
            "",
            $"Sitemap: {BaseUrl}/sitemap.xml"
        );
        robots.Should().Be(expected);
    }

    [Fact]
    public void SitemapXmlNoContentReturnsDeclarationAndStaticUrls()
    {
        var xml = SeoDocuments.SitemapXml(BaseUrl + "/", NoEntries);

        xml.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xml.Should().Contain("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        xml.Should().Contain($"<loc>{BaseUrl}/</loc>");
        xml.Should().Contain($"<loc>{BaseUrl}/about</loc>");
        xml.Should().Contain($"<loc>{BaseUrl}/events</loc>");
        xml.Should().Contain($"<loc>{BaseUrl}/news</loc>");
        xml.Should().Contain($"<loc>{BaseUrl}/resources</loc>");
        xml.Should().Contain($"<loc>{BaseUrl}/register</loc>");
        xml.Should().NotContain("<lastmod>");
    }

    [Fact]
    public void SitemapXmlContentEntriesRenderLocationAndUtcLastModifiedDay()
    {
        var eventId = Guid.NewGuid();
        var newsId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var entries = new SitemapEntries(
            [new SitemapEntry(eventId, new DateTimeOffset(2026, 2, 10, 18, 30, 0, TimeSpan.Zero))],
            [
                new SitemapEntry(
                    newsId,
                    new DateTimeOffset(2026, 3, 5, 0, 30, 0, TimeSpan.FromHours(2))
                ),
            ],
            [new SitemapEntry(resourceId, new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero))]
        );

        var xml = SeoDocuments.SitemapXml(BaseUrl, entries);

        xml.Should().Contain($"<loc>{BaseUrl}/events/{eventId}</loc>");
        xml.Should().Contain("<lastmod>2026-02-10</lastmod>");
        xml.Should().Contain($"<loc>{BaseUrl}/news/{newsId}</loc>");
        xml.Should().Contain("<lastmod>2026-03-04</lastmod>");
        xml.Should().Contain($"<loc>{BaseUrl}/resources/{resourceId}</loc>");
        xml.Should().Contain("<lastmod>2026-04-01</lastmod>");
    }
}
