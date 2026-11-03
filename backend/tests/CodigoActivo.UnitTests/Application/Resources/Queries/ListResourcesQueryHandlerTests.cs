using AwesomeAssertions;
using CodigoActivo.Application.Resources.Contracts;
using CodigoActivo.Application.Resources.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Resources.ResourceTestData;

namespace CodigoActivo.UnitTests.Application.Resources.Queries;

public sealed class ListResourcesQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly TestClock clock = new();
    private readonly ListResourcesQueryHandler sut;

    public ListResourcesQueryHandlerTests()
    {
        sut = new ListResourcesQueryHandler(store, new FakeQueryExecutor(), clock);
    }

    [Fact]
    public async Task HandleAsyncTitleFilterWithAccentMatchesCaseAndAccentInsensitively()
    {
        store.Resources.AddRange([NewResourceRow("Manual Ávila"), NewResourceRow("Otro")]);

        var result = await sut.HandleAsync(
            new ListResourcesQuery(new ResourceListQuery { Title = "avila" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Manual Ávila");
    }

    [Fact]
    public async Task HandleAsyncSubtitleFilterMatchesSubstring()
    {
        store.Resources.AddRange([
            NewResourceRow("A", subtitle: "documentación"),
            NewResourceRow("B", subtitle: "video"),
        ]);

        var result = await sut.HandleAsync(
            new ListResourcesQuery(new ResourceListQuery { Subtitle = "menta" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("A");
    }

    [Fact]
    public async Task HandleAsyncSearchMatchesTitleOrSubtitle()
    {
        store.Resources.AddRange([
            NewResourceRow("Guía de Scratch", subtitle: "Primeros pasos"),
            NewResourceRow("Fichas", subtitle: "Actividades con scratch"),
            NewResourceRow("Python", subtitle: "Introducción"),
        ]);

        var result = await sut.HandleAsync(
            new ListResourcesQuery(new ResourceListQuery { Search = "Scratch" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(r => r.Title).Should().BeEquivalentTo("Guía de Scratch", "Fichas");
    }

    [Fact]
    public async Task HandleAsyncExplicitTitleSortOrdersAscendingByTitle()
    {
        store.Resources.AddRange([
            NewResourceRow("Charlie"),
            NewResourceRow("Alpha"),
            NewResourceRow("Bravo"),
        ]);

        var result = await sut.HandleAsync(
            new ListResourcesQuery(new ResourceListQuery { Sort = "title" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(r => r.Title).Should().ContainInOrder("Alpha", "Bravo", "Charlie");
    }

    [Fact]
    public async Task HandleAsyncNoSortSpecifiedDefaultsToCreatedAtDescending()
    {
        store.Resources.AddRange([
            NewResourceRow("Old", year: 2022),
            NewResourceRow("Newest", year: 2026),
            NewResourceRow("Mid", year: 2024),
        ]);

        var result = await sut.HandleAsync(
            new ListResourcesQuery(new ResourceListQuery()),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(r => r.Title).Should().ContainInOrder("Newest", "Mid", "Old");
    }

    [Fact]
    public async Task HandleAsyncSortByUrlDescendingOrdersByUrlDescending()
    {
        store.Resources.AddRange([
            NewResourceRow("A", url: "https://a.es"),
            NewResourceRow("C", url: "https://c.es"),
            NewResourceRow("B", url: "https://b.es"),
        ]);

        var result = await sut.HandleAsync(
            new ListResourcesQuery(new ResourceListQuery { Sort = "-url" }),
            TestContext.Current.CancellationToken
        );

        result
            .Items.Select(r => r.Url)
            .Should()
            .ContainInOrder("https://c.es", "https://b.es", "https://a.es");
    }

    [Fact]
    public async Task HandleAsyncResourceHasTypeProjectsTypeAndUrl()
    {
        var resource = NewResourceRow(url: "https://ejemplo.es/recurso");
        store.Resources.Add(resource);

        var result = await sut.HandleAsync(
            new ListResourcesQuery(new ResourceListQuery()),
            TestContext.Current.CancellationToken
        );

        var item = result.Items.Should().ContainSingle().Subject;
        item.Url.Should().Be("https://ejemplo.es/recurso");
        item.Type.Id.Should().Be(resource.ResourceTypeId);
        item.Type.Name.Should().Be(resource.ResourceType.Name);
        item.Type.Color.Should().Be(resource.ResourceType.Color);
    }
}
