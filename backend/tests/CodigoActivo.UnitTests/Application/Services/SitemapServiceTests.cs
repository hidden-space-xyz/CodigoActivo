using AwesomeAssertions;
using CodigoActivo.Application.Services;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

public sealed class SitemapServiceTests
{
    private const string BaseUrl = "https://codigoactivo.test";

    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IAnnouncementRepository announcements =
        Substitute.For<IAnnouncementRepository>();
    private readonly IResourceRepository resources = Substitute.For<IResourceRepository>();
    private readonly SitemapService sut;

    public SitemapServiceTests()
    {
        events.Query().Returns(Array.Empty<Event>().AsQueryable());
        announcements.Query().Returns(Array.Empty<Announcement>().AsQueryable());
        resources.Query().Returns(Array.Empty<Resource>().AsQueryable());
        sut = new SitemapService(
            events,
            announcements,
            resources,
            new FakeQueryExecutor(),
            new ApplicationOptions { BaseUrl = BaseUrl + "/" },
            new FakeHybridCache()
        );
    }

    private static Event NewEvent(DateTimeOffset createdAt, DateTimeOffset? updatedAt = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = "Evento",
            Subtitle = "Sub",
            Description = "{}",
            EventStartsAt = new DateOnly(2026, 8, 1),
            EventEndsAt = new DateOnly(2026, 8, 2),
            SignupStartsAt = createdAt,
            SignupEndsAt = createdAt.AddDays(30),
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            CreatedBy = Guid.NewGuid(),
        };

    private static Announcement NewAnnouncement(DateTimeOffset createdAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = "Anuncio",
            Subtitle = "Sub",
            Description = "{}",
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = createdAt,
            CreatedBy = Guid.NewGuid(),
        };

    private static Resource NewResource(string? url) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = "Recurso",
            Subtitle = "Sub",
            Description = "{}",
            Url = url,
            ResourceTypeId = Guid.NewGuid(),
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
        };

    [Fact]
    public void GetRobotsTxt_TrailingSlashBaseUrl_ReturnsExactRulesWithTrimmedBase()
    {
        var robots = sut.GetRobotsTxt();

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
    public async Task GetSitemapXmlAsync_NoContent_ReturnsDeclarationAndStaticUrls()
    {
        var xml = await sut.GetSitemapXmlAsync(TestContext.Current.CancellationToken);

        xml.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xml.Should().Contain("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        xml.Should().Contain($"<loc>{BaseUrl}/</loc>");
        xml.Should().Contain($"<loc>{BaseUrl}/about</loc>");
        xml.Should().Contain($"<loc>{BaseUrl}/events</loc>");
        xml.Should().Contain($"<loc>{BaseUrl}/announcements</loc>");
        xml.Should().Contain($"<loc>{BaseUrl}/resources</loc>");
        xml.Should().Contain($"<loc>{BaseUrl}/register</loc>");
        xml.Should().NotContain("<lastmod>");
    }

    [Fact]
    public async Task GetSitemapXmlAsync_ExternalResource_ExcludesItsUrl()
    {
        var internalResource = NewResource(url: null);
        var externalResource = NewResource(url: "https://example.org/externo");
        resources.Query().Returns(new[] { internalResource, externalResource }.AsQueryable());

        var xml = await sut.GetSitemapXmlAsync(TestContext.Current.CancellationToken);

        xml.Should().Contain($"<loc>{BaseUrl}/resources/{internalResource.Id}</loc>");
        xml.Should().NotContain(externalResource.Id.ToString());
    }

    [Fact]
    public async Task GetSitemapXmlAsync_UpdatedAtPresent_UsesUpdatedAtAsLastmod()
    {
        var ev = NewEvent(
            createdAt: new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero),
            updatedAt: new DateTimeOffset(2026, 2, 10, 18, 30, 0, TimeSpan.Zero)
        );
        events.Query().Returns(new[] { ev }.AsQueryable());

        var xml = await sut.GetSitemapXmlAsync(TestContext.Current.CancellationToken);

        xml.Should().Contain($"<loc>{BaseUrl}/events/{ev.Id}</loc>");
        xml.Should().Contain("<lastmod>2026-02-10</lastmod>");
        xml.Should().NotContain("<lastmod>2026-01-05</lastmod>");
    }

    [Fact]
    public async Task GetSitemapXmlAsync_UpdatedAtMissing_FallsBackToCreatedAt()
    {
        var announcement = NewAnnouncement(new DateTimeOffset(2026, 3, 4, 23, 0, 0, TimeSpan.Zero));
        announcements.Query().Returns(new[] { announcement }.AsQueryable());

        var xml = await sut.GetSitemapXmlAsync(TestContext.Current.CancellationToken);

        xml.Should().Contain($"<loc>{BaseUrl}/announcements/{announcement.Id}</loc>");
        xml.Should().Contain("<lastmod>2026-03-04</lastmod>");
    }
}
