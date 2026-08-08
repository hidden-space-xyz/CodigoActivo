using AwesomeAssertions;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Resources.Queries;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Resources.ResourceTestData;

namespace CodigoActivo.UnitTests.Application.Resources.Queries;

public sealed class ListResourcesQueryHandlerTests
{
    private readonly IResourceRepository resources = Substitute.For<IResourceRepository>();
    private readonly TestClock clock = new();
    private readonly ListResourcesQueryHandler sut;

    public ListResourcesQueryHandlerTests()
    {
        sut = new ListResourcesQueryHandler(
            resources,
            new FakeQueryExecutor(),
            clock,
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsync_TitleFilterWithAccent_MatchesCaseAndAccentInsensitively()
    {
        resources.HasResources(NewResource("Manual Ávila"), NewResource("Otro"));

        var result = await sut.HandleAsync(
            new ListResourcesQuery(new ResourceListQuery { Title = "avila" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Manual Ávila");
    }

    [Fact]
    public async Task HandleAsync_SubtitleFilter_MatchesSubstring()
    {
        resources.HasResources(
            NewResource("A", subtitle: "documentación"),
            NewResource("B", subtitle: "video")
        );

        var result = await sut.HandleAsync(
            new ListResourcesQuery(new ResourceListQuery { Subtitle = "menta" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("A");
    }

    [Fact]
    public async Task HandleAsync_ExplicitTitleSort_OrdersAscendingByTitle()
    {
        resources.HasResources(NewResource("Charlie"), NewResource("Alpha"), NewResource("Bravo"));

        var result = await sut.HandleAsync(
            new ListResourcesQuery(new ResourceListQuery { Sort = "title" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(r => r.Title).Should().ContainInOrder("Alpha", "Bravo", "Charlie");
    }

    [Fact]
    public async Task HandleAsync_NoSortSpecified_DefaultsToCreatedAtDescending()
    {
        resources.HasResources(
            NewResource("Old", year: 2022),
            NewResource("Newest", year: 2026),
            NewResource("Mid", year: 2024)
        );

        var result = await sut.HandleAsync(
            new ListResourcesQuery(new ResourceListQuery()),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(r => r.Title).Should().ContainInOrder("Newest", "Mid", "Old");
    }

    [Fact]
    public async Task HandleAsync_ResourceTypeIdFilter_KeepsResourcesOfThatType()
    {
        var target = NewResource("Interno");
        resources.HasResources(target, NewResource("Otro"));

        var result = await sut.HandleAsync(
            new ListResourcesQuery(
                new ResourceListQuery { ResourceTypeId = target.ResourceTypeId }
            ),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Interno");
    }

    [Fact]
    public async Task HandleAsync_UrlFilter_IsAccentAndCaseInsensitive()
    {
        resources.HasResources(
            NewResource("Curso", url: "https://cursos.es/robótica"),
            NewResource("Otro", url: "https://cursos.es/ajedrez")
        );

        var result = await sut.HandleAsync(
            new ListResourcesQuery(new ResourceListQuery { Url = "ROBOTICA" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Curso");
    }

    [Fact]
    public async Task HandleAsync_CreatedRangeFilter_KeepsResourcesWithinDayBounds()
    {
        resources.HasResources(
            NewResource("Viejo", year: 2022),
            NewResource("Medio", year: 2024),
            NewResource("Nuevo", year: 2026)
        );

        var result = await sut.HandleAsync(
            new ListResourcesQuery(
                new ResourceListQuery
                {
                    CreatedFrom = new DateOnly(2023, 1, 1),
                    CreatedTo = new DateOnly(2025, 1, 1),
                }
            ),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Medio");
    }

    [Fact]
    public async Task HandleAsync_SortByType_OrdersByTypeName()
    {
        resources.HasResources(
            NewResource("Tercero", type: NewResourceType(name: "Video")),
            NewResource("Primero", type: NewResourceType(name: "Documento")),
            NewResource("Segundo", type: NewResourceType(name: "Enlace"))
        );

        var result = await sut.HandleAsync(
            new ListResourcesQuery(new ResourceListQuery { Sort = "type" }),
            TestContext.Current.CancellationToken
        );

        result
            .Items.Select(r => r.Type.Name)
            .Should()
            .ContainInOrder("Documento", "Enlace", "Video");
    }

    [Fact]
    public async Task HandleAsync_SortByUrlDescending_OrdersByUrlDescending()
    {
        resources.HasResources(
            NewResource("A", url: "https://a.es"),
            NewResource("C", url: "https://c.es"),
            NewResource("B", url: "https://b.es")
        );

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
    public async Task HandleAsync_ResourceHasType_ProjectsTypeAndUrl()
    {
        var resource = NewResource();
        resource.Url = "https://ejemplo.es/recurso";
        resources.HasResources(resource);

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
