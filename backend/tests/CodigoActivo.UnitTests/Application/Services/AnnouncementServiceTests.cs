using System.Linq.Expressions;
using AwesomeAssertions;
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

public sealed class AnnouncementServiceTests
{
    private readonly IAnnouncementRepository announcements =
        Substitute.For<IAnnouncementRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IFileService fileService = Substitute.For<IFileService>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly AnnouncementService sut;

    public AnnouncementServiceTests()
    {
        sut = new AnnouncementService(
            announcements,
            files,
            fileService,
            new FakeQueryExecutor(),
            clock,
            uow
        );
    }

    private void HasAnnouncements(params Announcement[] items) =>
        announcements.Query().Returns(items.AsQueryable());

    private void ThumbnailExists(bool exists) =>
        files
            .ExistsAsync(
                Arg.Any<Expression<Func<FileEntity, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(exists);

    private static Announcement NewAnnouncement(
        string title = "Hello",
        string subtitle = "World",
        bool featured = false,
        int year = 2024
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Subtitle = subtitle,
            Description = "{}",
            Featured = featured,
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
        };

    [Fact]
    public async Task ListAsync_filters_by_year()
    {
        HasAnnouncements(NewAnnouncement("Old", year: 2023), NewAnnouncement("New", year: 2025));

        var result = await sut.ListAsync(new AnnouncementListQuery { Year = 2025 });

        result.Items.Should().ContainSingle().Which.Title.Should().Be("New");
    }

    [Theory]
    [InlineData(true, "Star")]
    [InlineData(false, "Plain")]
    public async Task ListAsync_filters_by_featured(bool featured, string expected)
    {
        HasAnnouncements(
            NewAnnouncement("Star", featured: true),
            NewAnnouncement("Plain", featured: false)
        );

        var result = await sut.ListAsync(new AnnouncementListQuery { Featured = featured });

        result.Items.Should().ContainSingle().Which.Title.Should().Be(expected);
    }

    [Fact]
    public async Task ListAsync_title_search_is_accent_and_case_insensitive()
    {
        HasAnnouncements(NewAnnouncement("Reunión Ávila"), NewAnnouncement("Otra"));

        var result = await sut.ListAsync(new AnnouncementListQuery { Title = "avila" });

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Reunión Ávila");
    }

    [Fact]
    public async Task ListAsync_subtitle_search_matches_substring()
    {
        HasAnnouncements(
            NewAnnouncement("A", subtitle: "primavera"),
            NewAnnouncement("B", subtitle: "invierno")
        );

        var result = await sut.ListAsync(new AnnouncementListQuery { Subtitle = "vera" });

        result.Items.Should().ContainSingle().Which.Title.Should().Be("A");
    }

    [Fact]
    public async Task ListAsync_honours_explicit_ascending_title_sort()
    {
        HasAnnouncements(
            NewAnnouncement("Charlie"),
            NewAnnouncement("Alpha"),
            NewAnnouncement("Bravo")
        );

        var result = await sut.ListAsync(new AnnouncementListQuery { Sort = "title" });

        result.Items.Select(a => a.Title).Should().ContainInOrder("Alpha", "Bravo", "Charlie");
    }

    [Fact]
    public async Task GetByIdAsync_returns_not_found_when_missing()
    {
        HasAnnouncements();

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementNotFound);
    }

    [Fact]
    public async Task GetYearsAsync_returns_distinct_years_descending()
    {
        HasAnnouncements(
            NewAnnouncement(year: 2023),
            NewAnnouncement(year: 2025),
            NewAnnouncement(year: 2023),
            NewAnnouncement(year: 2024)
        );

        var result = await sut.GetYearsAsync();

        result.Should().ContainInOrder(2025, 2024, 2023);
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateAsync_fails_when_thumbnail_missing_and_does_not_persist()
    {
        ThumbnailExists(false);
        var request = new CreateAnnouncementRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.CreateAsync(request, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementThumbnailNotFound);
        await announcements.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task CreateAsync_persists_trimmed_announcement()
    {
        ThumbnailExists(true);
        var caller = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var request = new CreateAnnouncementRequest(
            "  Title  ",
            "  Subtitle  ",
            "{\"x\":1}",
            thumbnailId
        );

        var result = await sut.CreateAsync(request, caller);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Title");
        result.Value.Subtitle.Should().Be("Subtitle");
        result.Value.Description.Should().Be("{\"x\":1}");
        result.Value.ThumbnailId.Should().Be(thumbnailId);
        result.Value.CreatedBy.Should().Be(caller);
        result.Value.CreatedAt.Should().Be(clock.UtcNow);
        await announcements
            .Received(1)
            .AddAsync(
                Arg.Is<Announcement>(a =>
                    a.Title == "Title" && a.Subtitle == "Subtitle" && a.CreatedBy == caller
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_returns_not_found_when_missing()
    {
        announcements
            .FindAsync(
                Arg.Any<Expression<Func<Announcement, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((Announcement?)null);
        var request = new UpdateAnnouncementRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.UpdateAsync(Guid.NewGuid(), request, Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementNotFound);
        await files.DidNotReceiveWithAnyArgs().ExistsAsync(default!, default);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_returns_bad_request_when_thumbnail_missing()
    {
        var announcement = NewAnnouncement();
        announcements
            .FindAsync(
                Arg.Any<Expression<Func<Announcement, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(announcement);
        ThumbnailExists(false);
        var request = new UpdateAnnouncementRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.UpdateAsync(announcement.Id, request, Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_mutates_and_persists()
    {
        var announcement = NewAnnouncement("Old", "OldSub");
        announcements
            .FindAsync(
                Arg.Any<Expression<Func<Announcement, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(announcement);
        ThumbnailExists(true);
        var caller = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new UpdateAnnouncementRequest(
            "  New  ",
            "  NewSub  ",
            "{\"y\":2}",
            thumbnailId
        );

        var result = await sut.UpdateAsync(announcement.Id, request, caller);

        result.IsSuccess.Should().BeTrue();
        announcement.Title.Should().Be("New");
        announcement.Subtitle.Should().Be("NewSub");
        announcement.Description.Should().Be("{\"y\":2}");
        announcement.ThumbnailId.Should().Be(thumbnailId);
        announcement.UpdatedBy.Should().Be(caller);
        announcement.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_replacing_thumbnail_cleans_up_the_previous_file_after_save()
    {
        var announcement = NewAnnouncement();
        var previousThumbnailId = announcement.ThumbnailId;
        announcements
            .FindAsync(
                Arg.Any<Expression<Func<Announcement, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(announcement);
        ThumbnailExists(true);
        var request = new UpdateAnnouncementRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.UpdateAsync(announcement.Id, request, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        await fileService
            .Received(1)
            .DeleteIfOrphanedAsync(previousThumbnailId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_keeping_the_same_thumbnail_does_not_clean_up()
    {
        var announcement = NewAnnouncement();
        announcements
            .FindAsync(
                Arg.Any<Expression<Func<Announcement, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(announcement);
        ThumbnailExists(true);
        var request = new UpdateAnnouncementRequest(
            "Title",
            "Subtitle",
            "{}",
            announcement.ThumbnailId
        );

        var result = await sut.UpdateAsync(announcement.Id, request, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        await fileService.DidNotReceiveWithAnyArgs().DeleteIfOrphanedAsync(default, default);
    }

    [Fact]
    public async Task UpdateAsync_cleans_up_images_dropped_from_the_description_but_keeps_the_rest()
    {
        var announcement = NewAnnouncement();
        var removedId = Guid.NewGuid();
        var keptId = Guid.NewGuid();
        announcement.Description =
            $"{{\"a\":\"/api/files/{removedId}/content\",\"b\":\"/api/files/{keptId}/content\"}}";
        announcements
            .FindAsync(
                Arg.Any<Expression<Func<Announcement, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(announcement);
        ThumbnailExists(true);
        var request = new UpdateAnnouncementRequest(
            "Title",
            "Subtitle",
            $"{{\"b\":\"/api/files/{keptId}/content\"}}",
            announcement.ThumbnailId
        );

        var result = await sut.UpdateAsync(announcement.Id, request, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        await fileService
            .Received(1)
            .DeleteIfOrphanedAsync(removedId, Arg.Any<CancellationToken>());
        await fileService
            .DidNotReceive()
            .DeleteIfOrphanedAsync(keptId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_returns_not_found_when_announcement_missing()
    {
        announcements
            .FindAsync(
                Arg.Any<Expression<Func<Announcement, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((Announcement?)null);

        var result = await sut.DeleteAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await fileService.DidNotReceiveWithAnyArgs().DeleteIfOrphanedAsync(default, default);
    }

    [Fact]
    public async Task DeleteAsync_cleans_up_images_embedded_in_the_description()
    {
        var announcement = NewAnnouncement();
        var embeddedId = Guid.NewGuid();
        announcement.Description = $"{{\"img\":\"/api/files/{embeddedId}/content\"}}";
        announcements
            .FindAsync(
                Arg.Any<Expression<Func<Announcement, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(announcement);

        var result = await sut.DeleteAsync(announcement.Id);

        result.IsSuccess.Should().BeTrue();
        await fileService
            .Received(1)
            .DeleteIfOrphanedAsync(embeddedId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetFeaturedAsync_returns_not_found_when_id_missing()
    {
        announcements
            .SetFeaturedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await sut.SetFeaturedAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementNotFound);
    }

    [Fact]
    public async Task SetFeaturedAsync_returns_announcement_when_marked()
    {
        var announcement = NewAnnouncement("Featured", featured: true);
        announcements.SetFeaturedAsync(announcement.Id, Arg.Any<CancellationToken>()).Returns(true);
        HasAnnouncements(announcement);

        var result = await sut.SetFeaturedAsync(announcement.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(announcement.Id);
        result.Value.Featured.Should().BeTrue();
    }
}
