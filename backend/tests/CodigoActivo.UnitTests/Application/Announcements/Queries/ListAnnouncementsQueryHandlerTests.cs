using AwesomeAssertions;
using CodigoActivo.Application.Announcements.Queries;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Announcements.AnnouncementTestData;

namespace CodigoActivo.UnitTests.Application.Announcements.Queries;

public sealed class ListAnnouncementsQueryHandlerTests
{
    private readonly IAnnouncementRepository announcements =
        Substitute.For<IAnnouncementRepository>();
    private readonly TestClock clock = new();
    private readonly ListAnnouncementsQueryHandler sut;

    public ListAnnouncementsQueryHandlerTests()
    {
        sut = new ListAnnouncementsQueryHandler(
            announcements,
            new FakeQueryExecutor(),
            clock,
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsyncYearFilterReturnsMatchingYear()
    {
        announcements.HasAnnouncements(
            NewAnnouncement("Old", year: 2023),
            NewAnnouncement("New", year: 2025)
        );

        var result = await sut.HandleAsync(
            new ListAnnouncementsQuery(new AnnouncementListQuery { Year = 2025 }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("New");
    }

    [Fact]
    public async Task HandleAsyncYearOutOfRangeReturnsEmpty()
    {
        announcements.HasAnnouncements(NewAnnouncement("Any", year: 2025));

        var result = await sut.HandleAsync(
            new ListAnnouncementsQuery(new AnnouncementListQuery { Year = 0 }),
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncYearMaximumSupportedReturnsAnnouncementsOfYear9999()
    {
        announcements.HasAnnouncements(
            NewAnnouncement("Antiguo", year: 2025),
            NewAnnouncement("Futuro", year: 9999)
        );

        var result = await sut.HandleAsync(
            new ListAnnouncementsQuery(new AnnouncementListQuery { Year = 9999 }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle(a => a.Title == "Futuro");
    }

    [Fact]
    public async Task HandleAsyncCreatedRangeFilterKeepsAnnouncementsWithinDayBounds()
    {
        announcements.HasAnnouncements(
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

        var result = await sut.HandleAsync(
            new ListAnnouncementsQuery(
                new AnnouncementListQuery
                {
                    CreatedFrom = new DateOnly(2024, 5, 10),
                    CreatedTo = new DateOnly(2024, 5, 10),
                }
            ),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Dentro");
    }

    [Fact]
    public async Task HandleAsyncCreatedToFilterUsesAppTimeZoneDayEnd()
    {
        clock.TimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "UTC+02",
            TimeSpan.FromHours(2),
            "UTC+02",
            "UTC+02"
        );
        announcements.HasAnnouncements(
            NewAnnouncement(
                "Dentro",
                createdAt: new DateTimeOffset(2024, 5, 10, 21, 0, 0, TimeSpan.Zero)
            ),
            NewAnnouncement(
                "Fuera",
                createdAt: new DateTimeOffset(2024, 5, 10, 23, 0, 0, TimeSpan.Zero)
            )
        );

        var result = await sut.HandleAsync(
            new ListAnnouncementsQuery(
                new AnnouncementListQuery { CreatedTo = new DateOnly(2024, 5, 10) }
            ),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Dentro");
    }

    [Theory]
    [InlineData(true, "Star")]
    [InlineData(false, "Plain")]
    public async Task HandleAsyncFeaturedFilterReturnsMatchingFeaturedState(
        bool featured,
        string expected
    )
    {
        announcements.HasAnnouncements(
            NewAnnouncement("Star", featured: true),
            NewAnnouncement("Plain", featured: false)
        );

        var result = await sut.HandleAsync(
            new ListAnnouncementsQuery(new AnnouncementListQuery { Featured = featured }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be(expected);
    }

    [Fact]
    public async Task HandleAsyncTitleSearchIsAccentAndCaseInsensitive()
    {
        announcements.HasAnnouncements(NewAnnouncement("Reunión Ávila"), NewAnnouncement("Otra"));

        var result = await sut.HandleAsync(
            new ListAnnouncementsQuery(new AnnouncementListQuery { Title = "avila" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Reunión Ávila");
    }

    [Fact]
    public async Task HandleAsyncSubtitleSearchMatchesSubstring()
    {
        announcements.HasAnnouncements(
            NewAnnouncement("A", subtitle: "primavera"),
            NewAnnouncement("B", subtitle: "invierno")
        );

        var result = await sut.HandleAsync(
            new ListAnnouncementsQuery(new AnnouncementListQuery { Subtitle = "vera" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("A");
    }

    [Fact]
    public async Task HandleAsyncExplicitTitleSortOrdersAscending()
    {
        announcements.HasAnnouncements(
            NewAnnouncement("Charlie"),
            NewAnnouncement("Alpha"),
            NewAnnouncement("Bravo")
        );

        var result = await sut.HandleAsync(
            new ListAnnouncementsQuery(new AnnouncementListQuery { Sort = "title" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(a => a.Title).Should().ContainInOrder("Alpha", "Bravo", "Charlie");
    }

    [Fact]
    public async Task HandleAsyncEqualCreatedAtOrdersByIdTieBreakForStablePagination()
    {
        var first = NewAnnouncement("First");
        first.Id = new Guid("00000001-0000-0000-0000-000000000000");
        var second = NewAnnouncement("Second");
        second.Id = new Guid("00000002-0000-0000-0000-000000000000");
        var third = NewAnnouncement("Third");
        third.Id = new Guid("00000003-0000-0000-0000-000000000000");
        announcements.HasAnnouncements(third, first, second);

        var result = await sut.HandleAsync(
            new ListAnnouncementsQuery(new AnnouncementListQuery()),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(a => a.Id).Should().ContainInOrder(first.Id, second.Id, third.Id);
    }
}
