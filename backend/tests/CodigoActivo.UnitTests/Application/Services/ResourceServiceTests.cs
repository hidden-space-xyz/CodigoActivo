using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

public sealed class ResourceServiceTests
{
    private const string SomeRichText =
        "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"Contenido\"}]}]}";
    private const string EmptyRichText = "{\"type\":\"doc\",\"content\":[]}";

    private readonly IResourceRepository resources = Substitute.For<IResourceRepository>();
    private readonly IResourceTypeRepository resourceTypes =
        Substitute.For<IResourceTypeRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IFileService fileService = Substitute.For<IFileService>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly FakeHybridCache cache = new();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly ResourceService sut;

    public ResourceServiceTests()
    {
        sut = new ResourceService(
            resources,
            resourceTypes,
            files,
            fileService,
            new FakeQueryExecutor(),
            clock,
            uow,
            cache,
            cacheInvalidator
        );
    }

    private void HasResources(params Resource[] items) =>
        resources.Query().Returns(items.AsQueryable());

    private void HasTypes(params ResourceType[] items) =>
        resourceTypes.Query().Returns(items.AsQueryable());

    private void ThumbnailExists(bool exists) =>
        files
            .ExistsAsync(
                Arg.Any<Expression<Func<FileEntity, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(exists);

    private ResourceType TypeExists(bool isExternal = false)
    {
        var type = NewResourceType(isExternal);
        resourceTypes
            .FindAsync(
                Arg.Any<Expression<Func<ResourceType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(type);
        return type;
    }

    private void TypeMissing() =>
        resourceTypes
            .FindAsync(
                Arg.Any<Expression<Func<ResourceType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((ResourceType?)null);

    private static ResourceType NewResourceType(bool isExternal = false, string? name = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name ?? (isExternal ? "Externo" : "Interno"),
            Description = isExternal ? "Recurso enlazado" : "Recurso propio",
            Color = "#3B82F6",
            IsExternal = isExternal,
        };

    private static Resource NewResource(
        string title = "Guide",
        string subtitle = "Intro",
        int year = 2024,
        string? url = null,
        ResourceType? type = null
    )
    {
        var resourceType = type ?? NewResourceType();
        return new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Subtitle = subtitle,
            Description = SomeRichText,
            Url = url,
            ResourceTypeId = resourceType.Id,
            ResourceType = resourceType,
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
        };
    }

    [Fact]
    public async Task ListAsync_TitleFilterWithAccent_MatchesCaseAndAccentInsensitively()
    {
        HasResources(NewResource("Manual Ávila"), NewResource("Otro"));

        var result = await sut.ListAsync(
            new ResourceListQuery { Title = "avila" },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Manual Ávila");
    }

    [Fact]
    public async Task ListAsync_SubtitleFilter_MatchesSubstring()
    {
        HasResources(
            NewResource("A", subtitle: "documentación"),
            NewResource("B", subtitle: "video")
        );

        var result = await sut.ListAsync(
            new ResourceListQuery { Subtitle = "menta" },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("A");
    }

    [Fact]
    public async Task ListAsync_ExplicitTitleSort_OrdersAscendingByTitle()
    {
        HasResources(NewResource("Charlie"), NewResource("Alpha"), NewResource("Bravo"));

        var result = await sut.ListAsync(
            new ResourceListQuery { Sort = "title" },
            TestContext.Current.CancellationToken
        );

        result.Items.Select(r => r.Title).Should().ContainInOrder("Alpha", "Bravo", "Charlie");
    }

    [Fact]
    public async Task ListAsync_NoSortSpecified_DefaultsToCreatedAtDescending()
    {
        HasResources(
            NewResource("Old", year: 2022),
            NewResource("Newest", year: 2026),
            NewResource("Mid", year: 2024)
        );

        var result = await sut.ListAsync(
            new ResourceListQuery(),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(r => r.Title).Should().ContainInOrder("Newest", "Mid", "Old");
    }

    [Fact]
    public async Task ListAsync_ResourceTypeIdFilter_KeepsResourcesOfThatType()
    {
        var target = NewResource("Interno");
        HasResources(target, NewResource("Otro"));

        var result = await sut.ListAsync(
            new ResourceListQuery { ResourceTypeId = target.ResourceTypeId },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Interno");
    }

    [Fact]
    public async Task ListAsync_UrlFilter_IsAccentAndCaseInsensitive()
    {
        HasResources(
            NewResource("Curso", url: "https://cursos.es/robótica"),
            NewResource("Otro", url: "https://cursos.es/ajedrez")
        );

        var result = await sut.ListAsync(
            new ResourceListQuery { Url = "ROBOTICA" },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Curso");
    }

    [Fact]
    public async Task ListAsync_CreatedRangeFilter_KeepsResourcesWithinDayBounds()
    {
        HasResources(
            NewResource("Viejo", year: 2022),
            NewResource("Medio", year: 2024),
            NewResource("Nuevo", year: 2026)
        );

        var result = await sut.ListAsync(
            new ResourceListQuery
            {
                CreatedFrom = new DateOnly(2023, 1, 1),
                CreatedTo = new DateOnly(2025, 1, 1),
            },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Medio");
    }

    [Fact]
    public async Task ListAsync_SortByType_OrdersByTypeName()
    {
        HasResources(
            NewResource("Tercero", type: NewResourceType(name: "Video")),
            NewResource("Primero", type: NewResourceType(name: "Documento")),
            NewResource("Segundo", type: NewResourceType(name: "Enlace"))
        );

        var result = await sut.ListAsync(
            new ResourceListQuery { Sort = "type" },
            TestContext.Current.CancellationToken
        );

        result
            .Items.Select(r => r.Type.Name)
            .Should()
            .ContainInOrder("Documento", "Enlace", "Video");
    }

    [Fact]
    public async Task ListAsync_SortByUrlDescending_OrdersByUrlDescending()
    {
        HasResources(
            NewResource("A", url: "https://a.es"),
            NewResource("C", url: "https://c.es"),
            NewResource("B", url: "https://b.es")
        );

        var result = await sut.ListAsync(
            new ResourceListQuery { Sort = "-url" },
            TestContext.Current.CancellationToken
        );

        result
            .Items.Select(r => r.Url)
            .Should()
            .ContainInOrder("https://c.es", "https://b.es", "https://a.es");
    }

    [Fact]
    public async Task ListAsync_ResourceHasType_ProjectsTypeAndUrl()
    {
        var resource = NewResource();
        resource.Url = "https://ejemplo.es/recurso";
        HasResources(resource);

        var result = await sut.ListAsync(
            new ResourceListQuery(),
            TestContext.Current.CancellationToken
        );

        var item = result.Items.Should().ContainSingle().Subject;
        item.Url.Should().Be("https://ejemplo.es/recurso");
        item.Type.Id.Should().Be(resource.ResourceTypeId);
        item.Type.Name.Should().Be(resource.ResourceType.Name);
        item.Type.Color.Should().Be(resource.ResourceType.Color);
    }

    [Fact]
    public async Task ListTypesAsync_TypesExist_ReturnsTypesOrderedByName()
    {
        HasTypes(
            NewResourceType(isExternal: true, name: "Externo"),
            NewResourceType(name: "Interno")
        );

        var result = await sut.ListTypesAsync(TestContext.Current.CancellationToken);

        result.Select(t => t.Name).Should().ContainInOrder("Externo", "Interno");
        result.Select(t => t.IsExternal).Should().ContainInOrder(true, false);
    }

    [Fact]
    public async Task GetByIdAsync_ResourceExists_ReturnsResource()
    {
        var resource = NewResource();
        HasResources(resource);

        var result = await sut.GetByIdAsync(resource.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(resource.Id);
        result.Value.Type.Id.Should().Be(resource.ResourceTypeId);
    }

    [Fact]
    public async Task GetByIdAsync_ResourceMissing_ReturnsNotFound()
    {
        HasResources();

        var result = await sut.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ResourceNotFound);
    }

    [Fact]
    public async Task CreateAsync_ResourceTypeMissing_ReturnsBadRequestAndDoesNotPersist()
    {
        TypeMissing();
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceTypeNotFound);
        await resources
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_InternalWithUrl_ReturnsBadRequest()
    {
        var type = TypeExists();
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            "https://ejemplo.es",
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceUrlNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData(EmptyRichText)]
    public async Task CreateAsync_InternalWithEmptyDescription_ReturnsBadRequest(
        string? description
    )
    {
        var type = TypeExists();
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            description,
            null,
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceDescriptionRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_ExternalWithDescription_ReturnsBadRequest()
    {
        var type = TypeExists(isExternal: true);
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            "https://ejemplo.es",
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceDescriptionNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_ExternalWithoutUrl_ReturnsBadRequest()
    {
        var type = TypeExists(isExternal: true);
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            null,
            "   ",
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceUrlRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_ThumbnailMissing_ReturnsBadRequestAndDoesNotPersist()
    {
        var type = TypeExists();
        ThumbnailExists(false);
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceThumbnailNotFound);
        await resources
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_ValidInternalRequest_PersistsTrimmedResource()
    {
        var type = TypeExists();
        ThumbnailExists(true);
        var caller = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var request = new CreateResourceRequest(
            "  Title  ",
            "  Subtitle  ",
            SomeRichText,
            null,
            type.Id,
            thumbnailId
        );

        var result = await sut.CreateAsync(request, caller, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Title");
        result.Value.Subtitle.Should().Be("Subtitle");
        result.Value.Description.Should().Be(SomeRichText);
        result.Value.Url.Should().BeNull();
        result.Value.Type.Id.Should().Be(type.Id);
        result.Value.Type.IsExternal.Should().BeFalse();
        result.Value.ThumbnailId.Should().Be(thumbnailId);
        result.Value.CreatedBy.Should().Be(caller);
        result.Value.CreatedAt.Should().Be(clock.UtcNow);
        await resources
            .Received(1)
            .AddAsync(
                Arg.Is<Resource>(r =>
                    r.Title == "Title" && r.Subtitle == "Subtitle" && r.CreatedBy == caller
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ValidExternalRequest_PersistsTrimmedUrlAndEmptyDescription()
    {
        var type = TypeExists(isExternal: true);
        ThumbnailExists(true);
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            null,
            "  https://ejemplo.es/curso  ",
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Url.Should().Be("https://ejemplo.es/curso");
        result.Value.Description.Should().Be("{}");
        result.Value.Type.IsExternal.Should().BeTrue();
        await resources
            .Received(1)
            .AddAsync(
                Arg.Is<Resource>(r => r.Url == "https://ejemplo.es/curso" && r.Description == "{}"),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_InvalidatesResourcesCache()
    {
        var type = TypeExists();
        ThumbnailExists(true);
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags => tags.Contains(CacheTags.Resources))
            );
    }

    [Fact]
    public async Task UpdateAsync_ResourceMissing_ReturnsNotFound()
    {
        resources
            .FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Resource?)null);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ResourceNotFound);
        await files
            .DidNotReceiveWithAnyArgs()
            .ExistsAsync(default!, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ResourceTypeMissing_ReturnsBadRequest()
    {
        var resource = NewResource();
        resources
            .FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);
        TypeMissing();
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            Guid.NewGuid(),
            resource.ThumbnailId
        );

        var result = await sut.UpdateAsync(
            resource.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_InternalWithUrl_ReturnsBadRequest()
    {
        var resource = NewResource();
        resources
            .FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);
        var type = TypeExists();
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            "https://ejemplo.es",
            type.Id,
            resource.ThumbnailId
        );

        var result = await sut.UpdateAsync(
            resource.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceUrlNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ExternalWithDescription_ReturnsBadRequest()
    {
        var resource = NewResource();
        resources
            .FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);
        var type = TypeExists(isExternal: true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            "https://ejemplo.es",
            type.Id,
            resource.ThumbnailId
        );

        var result = await sut.UpdateAsync(
            resource.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceDescriptionNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ThumbnailMissing_ReturnsBadRequest()
    {
        var resource = NewResource();
        resources
            .FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);
        var type = TypeExists();
        ThumbnailExists(false);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.UpdateAsync(
            resource.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_MutatesAndPersistsResource()
    {
        var resource = NewResource("Old", "OldSub");
        resources
            .FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);
        var type = TypeExists();
        ThumbnailExists(true);
        var caller = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        const string NewDescription =
            "{\"type\":\"doc\",\"content\":[{\"type\":\"text\",\"text\":\"Nuevo\"}]}";
        var request = new UpdateResourceRequest(
            "  New  ",
            "  NewSub  ",
            NewDescription,
            null,
            type.Id,
            thumbnailId
        );

        var result = await sut.UpdateAsync(
            resource.Id,
            request,
            caller,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        resource.Title.Should().Be("New");
        resource.Subtitle.Should().Be("NewSub");
        resource.Description.Should().Be(NewDescription);
        resource.ResourceTypeId.Should().Be(type.Id);
        resource.ThumbnailId.Should().Be(thumbnailId);
        resource.UpdatedBy.Should().Be(caller);
        resource.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_InvalidatesResourcesCache()
    {
        var resource = NewResource();
        resources
            .FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);
        var type = TypeExists();
        ThumbnailExists(true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            type.Id,
            resource.ThumbnailId
        );

        var result = await sut.UpdateAsync(
            resource.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags => tags.Contains(CacheTags.Resources))
            );
    }

    [Fact]
    public async Task UpdateAsync_SwitchToExternal_ClearsDescriptionAndCleansEmbeddedImages()
    {
        var resource = NewResource();
        var embeddedId = Guid.NewGuid();
        resource.Description =
            $"{{\"text\":\"cuerpo\",\"img\":\"/api/files/{embeddedId}/content\"}}";
        resources
            .FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);
        var type = TypeExists(isExternal: true);
        ThumbnailExists(true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            null,
            "https://ejemplo.es/curso",
            type.Id,
            resource.ThumbnailId
        );

        var result = await sut.UpdateAsync(
            resource.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        resource.Description.Should().Be("{}");
        resource.Url.Should().Be("https://ejemplo.es/curso");
        resource.ResourceTypeId.Should().Be(type.Id);
        await fileService
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(embeddedId)),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task UpdateAsync_SwitchToInternal_ClearsUrl()
    {
        var resource = NewResource();
        resource.Description = "{}";
        resource.Url = "https://ejemplo.es/antiguo";
        resources
            .FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);
        var type = TypeExists();
        ThumbnailExists(true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            type.Id,
            resource.ThumbnailId
        );

        var result = await sut.UpdateAsync(
            resource.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        resource.Url.Should().BeNull();
        resource.Description.Should().Be(SomeRichText);
    }

    [Fact]
    public async Task UpdateAsync_ThumbnailReplaced_CleansUpPreviousThumbnailAfterSave()
    {
        var resource = NewResource();
        var previousThumbnailId = resource.ThumbnailId;
        resources
            .FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);
        var type = TypeExists();
        ThumbnailExists(true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.UpdateAsync(
            resource.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await fileService
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.Count == 1 && ids.Contains(previousThumbnailId)
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task UpdateAsync_ThumbnailUnchanged_DoesNotCleanUpThumbnail()
    {
        var resource = NewResource();
        resource.Description = SomeRichText;
        resources
            .FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);
        var type = TypeExists();
        ThumbnailExists(true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            type.Id,
            resource.ThumbnailId
        );

        var result = await sut.UpdateAsync(
            resource.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await fileService
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 0),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task UpdateAsync_ImageRemovedFromDescription_CleansUpRemovedImageOnly()
    {
        var resource = NewResource();
        var removedId = Guid.NewGuid();
        var keptId = Guid.NewGuid();
        resource.Description =
            $"{{\"text\":\"cuerpo\",\"a\":\"/api/files/{removedId}/content\",\"b\":\"/api/files/{keptId}/content\"}}";
        resources
            .FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);
        var type = TypeExists();
        ThumbnailExists(true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            $"{{\"text\":\"cuerpo\",\"b\":\"/api/files/{keptId}/content\"}}",
            null,
            type.Id,
            resource.ThumbnailId
        );

        var result = await sut.UpdateAsync(
            resource.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await fileService
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.Contains(removedId) && !ids.Contains(keptId)
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task DeleteAsync_ResourceMissing_ReturnsNotFound()
    {
        resources
            .FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Resource?)null);

        var result = await sut.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ResourceNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        await fileService
            .DidNotReceiveWithAnyArgs()
            .DeleteOrphanedAsync(default!, TestContext.Current.CancellationToken);
        await cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }

    [Fact]
    public async Task DeleteAsync_ResourceExists_RemovesSavesAndCleansUpThumbnail()
    {
        var resource = NewResource();
        resource.Description = "{}";
        resources
            .FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);

        var result = await sut.DeleteAsync(resource.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        resources.Received(1).Remove(resource);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await fileService
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(resource.ThumbnailId)),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task DeleteAsync_ResourceExists_InvalidatesResourcesCache()
    {
        var resource = NewResource();
        resource.Description = "{}";
        resources
            .FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);

        var result = await sut.DeleteAsync(resource.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags => tags.Contains(CacheTags.Resources))
            );
    }

    [Fact]
    public async Task DeleteAsync_DescriptionHasEmbeddedImages_CleansUpEmbeddedImages()
    {
        var resource = NewResource();
        var embeddedId = Guid.NewGuid();
        resource.Description = $"{{\"img\":\"/api/files/{embeddedId}/content\"}}";
        resources
            .FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);

        var result = await sut.DeleteAsync(resource.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await fileService
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.Contains(embeddedId) && ids.Contains(resource.ThumbnailId)
                ),
                Arg.Any<CancellationToken>()
            );
    }
}
