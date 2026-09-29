using AwesomeAssertions;
using CodigoActivo.Application.Seo;
using CodigoActivo.Application.Seo.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Seo.SeoTestData;

namespace CodigoActivo.UnitTests.Application.Seo.Queries;

public sealed class GetSitemapEntriesQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetSitemapEntriesQueryHandler sut;

    public GetSitemapEntriesQueryHandlerTests()
    {
        sut = new GetSitemapEntriesQueryHandler(store, new FakeQueryExecutor());
    }

    private Task<SitemapEntries> HandleAsync()
    {
        return sut.HandleAsync(new GetSitemapEntriesQuery(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncNoContentReturnsNoEntries()
    {
        var entries = await HandleAsync();

        entries.Events.Should().BeEmpty();
        entries.News.Should().BeEmpty();
        entries.Resources.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncExternalResourceIsLeftOut()
    {
        var internalResource = NewResourceRow(url: null);
        var externalResource = NewResourceRow(url: "https://example.org/externo");
        store.Resources.AddRange([internalResource, externalResource]);

        var entries = await HandleAsync();

        entries.Resources.Select(entry => entry.Id).Should().Equal(internalResource.Id);
    }

    [Fact]
    public async Task HandleAsyncUpdatedAtPresentUsesUpdatedAtAsLastModified()
    {
        var updatedAt = new DateTimeOffset(2026, 2, 10, 18, 30, 0, TimeSpan.Zero);
        var ev = NewEventRow(
            createdAt: new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero),
            updatedAt: updatedAt
        );
        store.Events.Add(ev);

        var entries = await HandleAsync();

        entries.Events.Should().Equal(new SitemapEntry(ev.Id, updatedAt));
    }

    [Fact]
    public async Task HandleAsyncUpdatedAtMissingFallsBackToCreatedAt()
    {
        var createdAt = new DateTimeOffset(2026, 3, 4, 23, 0, 0, TimeSpan.Zero);
        var newsItem = NewNewsItemRow(createdAt);
        store.News.Add(newsItem);

        var entries = await HandleAsync();

        entries.News.Should().Equal(new SitemapEntry(newsItem.Id, createdAt));
    }
}
