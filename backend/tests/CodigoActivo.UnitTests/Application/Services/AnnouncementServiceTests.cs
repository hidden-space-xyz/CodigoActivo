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

public sealed class AnnouncementServiceTests
{
    private readonly IAnnouncementRepository announcements =
        Substitute.For<IAnnouncementRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IFileService fileService = Substitute.For<IFileService>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly FakeHybridCache cache = new();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly AnnouncementService sut;

    public AnnouncementServiceTests()
    {
        sut = new AnnouncementService(
            announcements,
            files,
            fileService,
            new FakeQueryExecutor(),
            clock,
            uow,
            cache,
            cacheInvalidator
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
        int year = 2024,
        DateTimeOffset? createdAt = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Subtitle = subtitle,
            Description = "{}",
            Featured = featured,
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = createdAt ?? new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
        };

    [Fact]
    public async Task ListAsync_YearFilter_ReturnsMatchingYear()
    {
        HasAnnouncements(NewAnnouncement("Old", year: 2023), NewAnnouncement("New", year: 2025));

        var result = await sut.ListAsync(
            new AnnouncementListQuery { Year = 2025 },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("New");
    }

    [Fact]
    public async Task ListAsync_YearOutOfRange_ReturnsEmpty()
    {
        HasAnnouncements(NewAnnouncement("Any", year: 2025));

        var result = await sut.ListAsync(
            new AnnouncementListQuery { Year = 0 },
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_YearMaximumSupported_ReturnsAnnouncementsOfYear9999()
    {
        HasAnnouncements(
            NewAnnouncement("Antiguo", year: 2025),
            NewAnnouncement("Futuro", year: 9999)
        );

        var result = await sut.ListAsync(
            new AnnouncementListQuery { Year = 9999 },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle(a => a.Title == "Futuro");
    }

    [Fact]
    public async Task ListAsync_CreatedRangeFilter_KeepsAnnouncementsWithinDayBounds()
    {
        HasAnnouncements(
            NewAnnouncement(
                "Antes",
                createdAt: new DateTimeOffset(2024, 5, 9, 23, 59, 0, TimeSpan.Zero)
            ),
            NewAnnouncement(
                "Dentro",
                createdAt: new DateTimeOffset(2024, 5, 10, 12, 0, 0, TimeSpan.Zero)
            ),
            NewAnnouncement(
                "Despues",
                createdAt: new DateTimeOffset(2024, 5, 11, 0, 0, 0, TimeSpan.Zero)
            )
        );

        var result = await sut.ListAsync(
            new AnnouncementListQuery
            {
                CreatedFrom = new DateOnly(2024, 5, 10),
                CreatedTo = new DateOnly(2024, 5, 10),
            },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Dentro");
    }

    [Fact]
    public async Task ListAsync_CreatedToFilter_UsesAppTimeZoneDayEnd()
    {
        clock.TimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "UTC+02",
            TimeSpan.FromHours(2),
            "UTC+02",
            "UTC+02"
        );
        HasAnnouncements(
            NewAnnouncement(
                "Dentro",
                createdAt: new DateTimeOffset(2024, 5, 10, 21, 0, 0, TimeSpan.Zero)
            ),
            NewAnnouncement(
                "Fuera",
                createdAt: new DateTimeOffset(2024, 5, 10, 23, 0, 0, TimeSpan.Zero)
            )
        );

        var result = await sut.ListAsync(
            new AnnouncementListQuery { CreatedTo = new DateOnly(2024, 5, 10) },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Dentro");
    }

    [Theory]
    [InlineData(true, "Star")]
    [InlineData(false, "Plain")]
    public async Task ListAsync_FeaturedFilter_ReturnsMatchingFeaturedState(
        bool featured,
        string expected
    )
    {
        HasAnnouncements(
            NewAnnouncement("Star", featured: true),
            NewAnnouncement("Plain", featured: false)
        );

        var result = await sut.ListAsync(
            new AnnouncementListQuery { Featured = featured },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be(expected);
    }

    [Fact]
    public async Task ListAsync_TitleSearch_IsAccentAndCaseInsensitive()
    {
        HasAnnouncements(NewAnnouncement("Reunión Ávila"), NewAnnouncement("Otra"));

        var result = await sut.ListAsync(
            new AnnouncementListQuery { Title = "avila" },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Reunión Ávila");
    }

    [Fact]
    public async Task ListAsync_SubtitleSearch_MatchesSubstring()
    {
        HasAnnouncements(
            NewAnnouncement("A", subtitle: "primavera"),
            NewAnnouncement("B", subtitle: "invierno")
        );

        var result = await sut.ListAsync(
            new AnnouncementListQuery { Subtitle = "vera" },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("A");
    }

    [Fact]
    public async Task ListAsync_ExplicitTitleSort_OrdersAscending()
    {
        HasAnnouncements(
            NewAnnouncement("Charlie"),
            NewAnnouncement("Alpha"),
            NewAnnouncement("Bravo")
        );

        var result = await sut.ListAsync(
            new AnnouncementListQuery { Sort = "title" },
            TestContext.Current.CancellationToken
        );

        result.Items.Select(a => a.Title).Should().ContainInOrder("Alpha", "Bravo", "Charlie");
    }

    [Fact]
    public async Task ListAsync_EqualCreatedAt_OrdersByIdTieBreakForStablePagination()
    {
        var first = NewAnnouncement("First");
        first.Id = new Guid("00000001-0000-0000-0000-000000000000");
        var second = NewAnnouncement("Second");
        second.Id = new Guid("00000002-0000-0000-0000-000000000000");
        var third = NewAnnouncement("Third");
        third.Id = new Guid("00000003-0000-0000-0000-000000000000");
        HasAnnouncements(third, first, second);

        var result = await sut.ListAsync(
            new AnnouncementListQuery(),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(a => a.Id).Should().ContainInOrder(first.Id, second.Id, third.Id);
    }

    [Fact]
    public async Task GetByIdAsync_AnnouncementMissing_ReturnsNotFound()
    {
        HasAnnouncements();

        var result = await sut.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementNotFound);
    }

    [Fact]
    public async Task GetYearsAsync_DuplicateYears_ReturnsDistinctDescending()
    {
        HasAnnouncements(
            NewAnnouncement(year: 2023),
            NewAnnouncement(year: 2025),
            NewAnnouncement(year: 2023),
            NewAnnouncement(year: 2024)
        );

        var result = await sut.GetYearsAsync(TestContext.Current.CancellationToken);

        result.Should().ContainInOrder(2025, 2024, 2023);
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateAsync_ThumbnailMissing_FailsAndDoesNotPersist()
    {
        ThumbnailExists(false);
        var request = new CreateAnnouncementRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementThumbnailNotFound);
        await announcements
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsTrimmedAnnouncement()
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

        var result = await sut.CreateAsync(request, caller, TestContext.Current.CancellationToken);

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
    public async Task CreateAsync_ValidRequest_InvalidatesAnnouncementsCache()
    {
        ThumbnailExists(true);
        var request = new CreateAnnouncementRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags => tags.Contains(CacheTags.Announcements))
            );
    }

    [Fact]
    public async Task UpdateAsync_AnnouncementMissing_ReturnsNotFound()
    {
        announcements
            .FindAsync(
                Arg.Any<Expression<Func<Announcement, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((Announcement?)null);
        var request = new UpdateAnnouncementRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementNotFound);
        await files
            .DidNotReceiveWithAnyArgs()
            .ExistsAsync(default!, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        await cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }

    [Fact]
    public async Task UpdateAsync_ThumbnailMissing_ReturnsBadRequest()
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

        var result = await sut.UpdateAsync(
            announcement.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_MutatesAndPersists()
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

        var result = await sut.UpdateAsync(
            announcement.Id,
            request,
            caller,
            TestContext.Current.CancellationToken
        );

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
    public async Task UpdateAsync_ValidRequest_InvalidatesAnnouncementsCache()
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

        var result = await sut.UpdateAsync(
            announcement.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags => tags.Contains(CacheTags.Announcements))
            );
    }

    [Fact]
    public async Task UpdateAsync_ThumbnailReplaced_CleansUpPreviousFileAfterSave()
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

        var result = await sut.UpdateAsync(
            announcement.Id,
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
    public async Task UpdateAsync_ThumbnailUnchanged_DoesNotCleanUp()
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

        var result = await sut.UpdateAsync(
            announcement.Id,
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
    public async Task UpdateAsync_ImagesDroppedFromDescription_CleansUpDroppedKeepsRest()
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

        var result = await sut.UpdateAsync(
            announcement.Id,
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
    public async Task DeleteAsync_AnnouncementMissing_ReturnsNotFound()
    {
        announcements
            .FindAsync(
                Arg.Any<Expression<Func<Announcement, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((Announcement?)null);

        var result = await sut.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        await fileService
            .DidNotReceiveWithAnyArgs()
            .DeleteOrphanedAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeleteAsync_ImagesEmbeddedInDescription_CleansUp()
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

        var result = await sut.DeleteAsync(announcement.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await fileService
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.Contains(embeddedId) && ids.Contains(announcement.ThumbnailId)
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task DeleteAsync_AnnouncementExists_InvalidatesAnnouncementsCache()
    {
        var announcement = NewAnnouncement();
        announcement.Description = "{}";
        announcements
            .FindAsync(
                Arg.Any<Expression<Func<Announcement, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(announcement);

        var result = await sut.DeleteAsync(announcement.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags => tags.Contains(CacheTags.Announcements))
            );
    }

    [Fact]
    public async Task SetFeaturedAsync_IdMissing_ReturnsNotFound()
    {
        announcements
            .SetFeaturedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await sut.SetFeaturedAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementNotFound);
    }

    [Fact]
    public async Task SetFeaturedAsync_Marked_ReturnsFeaturedAnnouncement()
    {
        var announcement = NewAnnouncement("Featured", featured: true);
        announcements.SetFeaturedAsync(announcement.Id, Arg.Any<CancellationToken>()).Returns(true);
        HasAnnouncements(announcement);

        var result = await sut.SetFeaturedAsync(
            announcement.Id,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(announcement.Id);
        result.Value.Featured.Should().BeTrue();
    }

    [Fact]
    public async Task SetFeaturedAsync_Marked_InvalidatesAnnouncementsCache()
    {
        var announcement = NewAnnouncement("Featured", featured: true);
        announcements.SetFeaturedAsync(announcement.Id, Arg.Any<CancellationToken>()).Returns(true);
        HasAnnouncements(announcement);

        var result = await sut.SetFeaturedAsync(
            announcement.Id,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags => tags.Contains(CacheTags.Announcements))
            );
    }
}
