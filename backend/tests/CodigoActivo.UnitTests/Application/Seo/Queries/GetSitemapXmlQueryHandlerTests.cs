using AwesomeAssertions;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Seo.Queries;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Seo.SeoTestData;

namespace CodigoActivo.UnitTests.Application.Seo.Queries;

public sealed class GetSitemapXmlQueryHandlerTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IAnnouncementRepository announcements =
        Substitute.For<IAnnouncementRepository>();
    private readonly IResourceRepository resources = Substitute.For<IResourceRepository>();
    private readonly GetSitemapXmlQueryHandler sut;

    public GetSitemapXmlQueryHandlerTests()
    {
        events.Query().Returns(Array.Empty<Event>().AsQueryable());
        announcements.Query().Returns(Array.Empty<Announcement>().AsQueryable());
        resources.Query().Returns(Array.Empty<Resource>().AsQueryable());
        sut = new GetSitemapXmlQueryHandler(
            events,
            announcements,
            resources,
            new FakeQueryExecutor(),
            new ApplicationOptions { BaseUrl = BaseUrl + "/" },
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsync_NoContent_ReturnsDeclarationAndStaticUrls()
    {
        var xml = await sut.HandleAsync(
            new GetSitemapXmlQuery(),
            TestContext.Current.CancellationToken
        );

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
    public async Task HandleAsync_ExternalResource_ExcludesItsUrl()
    {
        var internalResource = NewResource(url: null);
        var externalResource = NewResource(url: "https://example.org/externo");
        resources.Query().Returns(new[] { internalResource, externalResource }.AsQueryable());

        var xml = await sut.HandleAsync(
            new GetSitemapXmlQuery(),
            TestContext.Current.CancellationToken
        );

        xml.Should().Contain($"<loc>{BaseUrl}/resources/{internalResource.Id}</loc>");
        xml.Should().NotContain(externalResource.Id.ToString());
    }

    [Fact]
    public async Task HandleAsync_UpdatedAtPresent_UsesUpdatedAtAsLastmod()
    {
        var ev = NewEvent(
            createdAt: new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero),
            updatedAt: new DateTimeOffset(2026, 2, 10, 18, 30, 0, TimeSpan.Zero)
        );
        events.Query().Returns(new[] { ev }.AsQueryable());

        var xml = await sut.HandleAsync(
            new GetSitemapXmlQuery(),
            TestContext.Current.CancellationToken
        );

        xml.Should().Contain($"<loc>{BaseUrl}/events/{ev.Id}</loc>");
        xml.Should().Contain("<lastmod>2026-02-10</lastmod>");
        xml.Should().NotContain("<lastmod>2026-01-05</lastmod>");
    }

    [Fact]
    public async Task HandleAsync_UpdatedAtMissing_FallsBackToCreatedAt()
    {
        var announcement = NewAnnouncement(new DateTimeOffset(2026, 3, 4, 23, 0, 0, TimeSpan.Zero));
        announcements.Query().Returns(new[] { announcement }.AsQueryable());

        var xml = await sut.HandleAsync(
            new GetSitemapXmlQuery(),
            TestContext.Current.CancellationToken
        );

        xml.Should().Contain($"<loc>{BaseUrl}/announcements/{announcement.Id}</loc>");
        xml.Should().Contain("<lastmod>2026-03-04</lastmod>");
    }
}
