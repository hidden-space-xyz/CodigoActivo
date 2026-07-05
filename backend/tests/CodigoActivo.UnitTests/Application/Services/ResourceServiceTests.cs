using System.Linq.Expressions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

/// <summary>
/// Unit tests for <see cref="ResourceService"/>. Collaborators are NSubstitute doubles; the read path
/// runs against a real <see cref="FakeQueryExecutor"/> over <c>list.AsQueryable()</c>, exercising the
/// projection, <see cref="SortMap{T}"/> and <see cref="TextSearch"/> expressions for real.
/// </summary>
public sealed class ResourceServiceTests
{
    private readonly IResourceRepository resources = Substitute.For<IResourceRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IFileService fileService = Substitute.For<IFileService>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly ResourceService sut;

    public ResourceServiceTests()
    {
        sut = new ResourceService(resources, files, fileService, new FakeQueryExecutor(), clock, uow);
    }

    private void HasResources(params Resource[] items) =>
        resources.Query().Returns(items.AsQueryable());

    private void ThumbnailExists(bool exists) =>
        files
            .ExistsAsync(Arg.Any<Expression<Func<FileEntity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(exists);

    private static Resource NewResource(
        string title = "Guide",
        string subtitle = "Intro",
        int year = 2024
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Subtitle = subtitle,
            Description = "{}",
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
        };

    // ---- ListAsync ---------------------------------------------------------

    [Fact]
    public async Task ListAsync_projects_and_pages()
    {
        HasResources(NewResource("A"), NewResource("B"));

        var result = await sut.ListAsync(new ResourceListQuery { Page = 1, PageSize = 10 });

        result.Total.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().AllBeOfType<ResourceListItemResponse>();
    }

    [Fact]
    public async Task ListAsync_title_search_is_accent_and_case_insensitive()
    {
        HasResources(NewResource("Manual Ávila"), NewResource("Otro"));

        var result = await sut.ListAsync(new ResourceListQuery { Title = "avila" });

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Manual Ávila");
    }

    [Fact]
    public async Task ListAsync_subtitle_search_matches_substring()
    {
        HasResources(
            NewResource("A", subtitle: "documentación"),
            NewResource("B", subtitle: "video")
        );

        var result = await sut.ListAsync(new ResourceListQuery { Subtitle = "menta" });

        result.Items.Should().ContainSingle().Which.Title.Should().Be("A");
    }

    [Fact]
    public async Task ListAsync_honours_explicit_ascending_title_sort()
    {
        HasResources(NewResource("Charlie"), NewResource("Alpha"), NewResource("Bravo"));

        var result = await sut.ListAsync(new ResourceListQuery { Sort = "title" });

        result.Items.Select(r => r.Title).Should().ContainInOrder("Alpha", "Bravo", "Charlie");
    }

    [Fact]
    public async Task ListAsync_defaults_to_created_at_descending()
    {
        HasResources(
            NewResource("Old", year: 2022),
            NewResource("Newest", year: 2026),
            NewResource("Mid", year: 2024)
        );

        var result = await sut.ListAsync(new ResourceListQuery());

        result.Items.Select(r => r.Title).Should().ContainInOrder("Newest", "Mid", "Old");
    }

    // ---- GetByIdAsync ------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_returns_resource_when_found()
    {
        var resource = NewResource();
        HasResources(resource);

        var result = await sut.GetByIdAsync(resource.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(resource.Id);
    }

    [Fact]
    public async Task GetByIdAsync_returns_not_found_when_missing()
    {
        HasResources();

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ResourceNotFound);
    }

    // ---- CreateAsync -------------------------------------------------------

    [Fact]
    public async Task CreateAsync_fails_when_thumbnail_missing_and_does_not_persist()
    {
        ThumbnailExists(false);
        var request = new CreateResourceRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.CreateAsync(request, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceThumbnailNotFound);
        await resources.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task CreateAsync_persists_trimmed_resource()
    {
        ThumbnailExists(true);
        var caller = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var request = new CreateResourceRequest("  Title  ", "  Subtitle  ", "{\"x\":1}", thumbnailId);

        var result = await sut.CreateAsync(request, caller);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Title");
        result.Value.Subtitle.Should().Be("Subtitle");
        result.Value.Description.Should().Be("{\"x\":1}");
        result.Value.ThumbnailId.Should().Be(thumbnailId);
        result.Value.CreatedBy.Should().Be(caller);
        result.Value.CreatedAt.Should().Be(clock.UtcNow);
        await resources.Received(1).AddAsync(
            Arg.Is<Resource>(r =>
                r.Title == "Title" && r.Subtitle == "Subtitle" && r.CreatedBy == caller
            ),
            Arg.Any<CancellationToken>()
        );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---- UpdateAsync -------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_returns_not_found_when_missing()
    {
        resources.FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Resource?)null);
        var request = new UpdateResourceRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.UpdateAsync(Guid.NewGuid(), request, Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ResourceNotFound);
        await files.DidNotReceiveWithAnyArgs().ExistsAsync(default!, default);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_returns_bad_request_when_thumbnail_missing()
    {
        var resource = NewResource();
        resources.FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);
        ThumbnailExists(false);
        var request = new UpdateResourceRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.UpdateAsync(resource.Id, request, Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_mutates_and_persists()
    {
        var resource = NewResource("Old", "OldSub");
        resources.FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);
        ThumbnailExists(true);
        var caller = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new UpdateResourceRequest("  New  ", "  NewSub  ", "{\"y\":2}", thumbnailId);

        var result = await sut.UpdateAsync(resource.Id, request, caller);

        result.IsSuccess.Should().BeTrue();
        resource.Title.Should().Be("New");
        resource.Subtitle.Should().Be("NewSub");
        resource.Description.Should().Be("{\"y\":2}");
        resource.ThumbnailId.Should().Be(thumbnailId);
        resource.UpdatedBy.Should().Be(caller);
        resource.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_replacing_thumbnail_cleans_up_the_previous_file_after_save()
    {
        var resource = NewResource();
        var previousThumbnailId = resource.ThumbnailId;
        resources.FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);
        ThumbnailExists(true);
        var request = new UpdateResourceRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.UpdateAsync(resource.Id, request, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        await fileService.Received(1).DeleteIfOrphanedAsync(previousThumbnailId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_keeping_the_same_thumbnail_does_not_clean_up()
    {
        var resource = NewResource();
        resources.FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);
        ThumbnailExists(true);
        var request = new UpdateResourceRequest("Title", "Subtitle", "{}", resource.ThumbnailId);

        var result = await sut.UpdateAsync(resource.Id, request, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        await fileService.DidNotReceiveWithAnyArgs().DeleteIfOrphanedAsync(default, default);
    }

    [Fact]
    public async Task UpdateAsync_cleans_up_images_dropped_from_the_description_but_keeps_the_rest()
    {
        var resource = NewResource();
        var removedId = Guid.NewGuid();
        var keptId = Guid.NewGuid();
        resource.Description =
            $"{{\"a\":\"/api/files/{removedId}/content\",\"b\":\"/api/files/{keptId}/content\"}}";
        resources.FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);
        ThumbnailExists(true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            $"{{\"b\":\"/api/files/{keptId}/content\"}}",
            resource.ThumbnailId
        );

        var result = await sut.UpdateAsync(resource.Id, request, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        await fileService.Received(1).DeleteIfOrphanedAsync(removedId, Arg.Any<CancellationToken>());
        await fileService.DidNotReceive().DeleteIfOrphanedAsync(keptId, Arg.Any<CancellationToken>());
    }

    // ---- DeleteAsync -------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_returns_not_found_when_resource_missing()
    {
        resources.FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Resource?)null);

        var result = await sut.DeleteAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ResourceNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await fileService.DidNotReceiveWithAnyArgs().DeleteIfOrphanedAsync(default, default);
    }

    [Fact]
    public async Task DeleteAsync_removes_saves_and_cleans_up_the_thumbnail()
    {
        var resource = NewResource();
        resources.FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);

        var result = await sut.DeleteAsync(resource.Id);

        result.IsSuccess.Should().BeTrue();
        resources.Received(1).Remove(resource);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await fileService.Received(1).DeleteIfOrphanedAsync(resource.ThumbnailId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_cleans_up_images_embedded_in_the_description()
    {
        var resource = NewResource();
        var embeddedId = Guid.NewGuid();
        resource.Description = $"{{\"img\":\"/api/files/{embeddedId}/content\"}}";
        resources.FindAsync(Arg.Any<Expression<Func<Resource, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(resource);

        var result = await sut.DeleteAsync(resource.Id);

        result.IsSuccess.Should().BeTrue();
        await fileService.Received(1).DeleteIfOrphanedAsync(embeddedId, Arg.Any<CancellationToken>());
    }
}
