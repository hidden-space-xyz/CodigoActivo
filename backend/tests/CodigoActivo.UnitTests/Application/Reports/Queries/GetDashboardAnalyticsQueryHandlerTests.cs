using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Reports.Queries;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Reports.ReportTestData;

namespace CodigoActivo.UnitTests.Application.Reports.Queries;

public sealed class GetDashboardAnalyticsQueryHandlerTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IResourceRepository resources = Substitute.For<IResourceRepository>();
    private readonly INewsItemRepository news = Substitute.For<INewsItemRepository>();
    private readonly IPartnerRepository partners = Substitute.For<IPartnerRepository>();
    private readonly IEventCategoryTypeRepository eventCategoryTypes =
        Substitute.For<IEventCategoryTypeRepository>();
    private readonly TestClock clock = new(
        new DateTimeOffset(2026, 7, 7, 10, 0, 0, TimeSpan.Zero),
        new DateOnly(2026, 7, 7)
    );
    private readonly GetDashboardAnalyticsQueryHandler sut;

    public GetDashboardAnalyticsQueryHandlerTests()
    {
        sut = new GetDashboardAnalyticsQueryHandler(
            events,
            activities,
            users,
            resources,
            news,
            partners,
            eventCategoryTypes,
            new FakeQueryExecutor(),
            clock,
            new FakeHybridCache()
        );
    }

    private void HasActivityRows(params Activity[] list)
    {
        activities.Query().Returns(list.AsQueryable());
    }

    private void HasResources(params Resource[] list)
    {
        resources.Query().Returns(list.AsQueryable());
    }

    private void HasNews(params NewsItem[] list)
    {
        news.Query().Returns(list.AsQueryable());
    }

    private void HasPartners(params Partner[] list)
    {
        partners.Query().Returns(list.AsQueryable());
    }

    private void HasCategoryTypes(params EventCategoryType[] list)
    {
        eventCategoryTypes.Query().Returns(list.AsQueryable());
    }

    private static User AnalyticsUser(
        Guid typeId,
        Guid statusId,
        DateTimeOffset createdAt,
        Guid? parentId = null,
        Gender gender = Gender.Female
    )
    {
        return new()
        {
            Id = Guid.NewGuid(),
            FirstName = "U",
            LastName = "U",
            CreatedAt = createdAt,
            UserTypeId = typeId,
            UserStatusTypeId = statusId,
            ParentId = parentId,
            Gender = gender,
        };
    }

    private static ActivityUserRoleAssignment Insc(
        Guid eventId,
        Guid statusId,
        DateTimeOffset createdAt
    )
    {
        return new()
        {
            UserId = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            Activity = new Activity
            {
                Description = "Descripción de la actividad",
                Location = "Sala principal",
                Title = "Actividad de prueba",
                EventId = eventId,
            },
            AssignmentStatusId = statusId,
            CreatedAt = createdAt,
        };
    }

    private static Activity AnalyticsActivity(
        DateTimeOffset startsAt,
        DateTimeOffset createdAt,
        int desired,
        int confirmed,
        Guid eventId,
        string eventTitle = "Evento",
        string title = "Actividad"
    )
    {
        var activity = new Activity
        {
            Description = "Descripción de la actividad",
            Location = "Sala principal",
            Id = Guid.NewGuid(),
            Title = title,
            ActivityStartsAt = startsAt,
            CreatedAt = createdAt,
            EventId = eventId,
            Event = new Event
            {
                Subtitle = "Subtítulo del evento",
                Id = eventId,
                Title = eventTitle,
            },
        };
        if (desired > 0)
        {
            activity.RoleCapacities.Add(
                new ActivityRoleCapacity
                {
                    DesiredCount = desired,
                    ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                }
            );
        }

        for (var i = 0; i < confirmed; i++)
        {
            activity.Assignments.Add(
                new ActivityUserRoleAssignment { AssignmentStatusId = Confirmed }
            );
        }

        return activity;
    }

    private static DashboardKpiResponse Kpi(DashboardAnalyticsResponse r, string key)
    {
        return r.Kpis.Single(k => string.Equals(k.Key, key, StringComparison.Ordinal));
    }

    private static int Slice(IReadOnlyList<DashboardSliceResponse> slices, string key)
    {
        return slices.Single(s => string.Equals(s.Key, key, StringComparison.Ordinal)).Count;
    }

    private static IReadOnlyList<int> Series(DashboardTimeSeriesResponse ts, string key)
    {
        return ts.Series.Single(s => string.Equals(s.Key, key, StringComparison.Ordinal)).Values;
    }

    [Fact]
    public async Task HandleAsyncMixedDataProducesExpectedSeriesAndBreakdowns()
    {
        var e1 = new Guid("eeeeeeee-0000-0000-0000-000000000001");
        var e2 = new Guid("eeeeeeee-0000-0000-0000-000000000002");

        var member = SeedIds.UserTypes.Member;
        var sponsor = SeedIds.UserTypes.Sponsor;
        var participant = SeedIds.UserTypes.Participant;
        var active = SeedIds.UserStatusTypes.Active;
        var dependent = SeedIds.UserStatusTypes.Dependent;

        var parent = AnalyticsUser(participant, active, Utc(2026, 6, 20), gender: Gender.Other);
        users.HasUsers(
            AnalyticsUser(member, active, Utc(2025, 12, 1)),
            AnalyticsUser(member, active, Utc(2026, 2, 15)),
            AnalyticsUser(sponsor, active, Utc(2026, 3, 10), gender: Gender.Male),
            parent,
            AnalyticsUser(participant, dependent, Utc(2026, 6, 25), parent.Id, Gender.Male)
        );

        activities.HasAssignments(
            Insc(e1, Confirmed, Utc(2026, 2, 10)),
            Insc(e1, Confirmed, Utc(2026, 2, 20)),
            Insc(e1, Requested, Utc(2026, 3, 5)),
            Insc(e2, Denied, Utc(2026, 6, 1)),
            Insc(e2, Confirmed, Utc(2026, 6, 15))
        );

        events.HasEvents(
            new Event
            {
                Subtitle = "Subtítulo del evento",
                Id = e1,
                Title = "Feria",
                CreatedAt = Utc(2026, 1, 5),
                EventStartsAt = new DateOnly(2026, 2, 1),
            },
            new Event
            {
                Subtitle = "Subtítulo del evento",
                Id = e2,
                Title = "Taller",
                CreatedAt = Utc(2026, 5, 10),
                EventStartsAt = new DateOnly(2026, 9, 1),
            }
        );

        HasCategoryTypes(
            new EventCategoryType
            {
                Id = Guid.NewGuid(),
                Name = "Formación",
                Color = "#F97316",
                Events = [new EventCategory(), new EventCategory()],
            },
            new EventCategoryType
            {
                Id = Guid.NewGuid(),
                Name = "Robótica",
                Color = "#84CC16",
                Events = [new EventCategory()],
            }
        );

        HasResources(
            new Resource
            {
                Subtitle = "Subtítulo del recurso",
                Title = "Recurso de prueba",
                CreatedAt = Utc(2026, 3, 1),
                ResourceTypeId = SeedIds.ResourceTypes.Internal,
            },
            new Resource
            {
                Subtitle = "Subtítulo del recurso",
                Title = "Recurso de prueba",
                CreatedAt = Utc(2026, 4, 1),
                ResourceTypeId = SeedIds.ResourceTypes.External,
            },
            new Resource
            {
                Subtitle = "Subtítulo del recurso",
                Title = "Recurso de prueba",
                CreatedAt = Utc(2025, 12, 15),
                ResourceTypeId = SeedIds.ResourceTypes.External,
            }
        );

        HasNews(
            new NewsItem
            {
                Subtitle = "Subtítulo de la noticia",
                Title = "Noticia de prueba",
                CreatedAt = Utc(2026, 2, 1),
            },
            new NewsItem
            {
                Subtitle = "Subtítulo de la noticia",
                Title = "Noticia de prueba",
                CreatedAt = Utc(2026, 5, 1),
            }
        );

        HasPartners(new Partner { Name = "Entidad colaboradora", CreatedAt = Utc(2026, 1, 10) });

        var occEventId = new Guid("cccccccc-0000-0000-0000-000000000001");
        HasActivityRows(
            AnalyticsActivity(
                Utc(2026, 9, 1),
                Utc(2026, 5, 1),
                desired: 5,
                confirmed: 3,
                eventId: occEventId,
                eventTitle: "Robótica",
                title: "Taller de septiembre"
            ),
            AnalyticsActivity(
                Utc(2026, 2, 1),
                Utc(2026, 1, 1),
                desired: 2,
                confirmed: 2,
                eventId: new Guid("cccccccc-0000-0000-0000-000000000002"),
                eventTitle: "Evento pasado"
            )
        );

        var r = await sut.HandleAsync(
            new GetDashboardAnalyticsQuery(
                new DashboardAnalyticsQuery
                {
                    From = new DateOnly(2026, 1, 1),
                    To = new DateOnly(2026, 12, 31),
                }
            ),
            TestContext.Current.CancellationToken
        );

        r.Granularity.Should().Be("month");
        r.UserGrowth.Buckets.Should().HaveCount(12);

        Kpi(r, "users").Should().BeEquivalentTo(new DashboardKpiResponse("users", 5, 4, 1));
        Kpi(r, "members").Should().BeEquivalentTo(new DashboardKpiResponse("members", 2, 1, 1));
        Kpi(r, "inscriptions")
            .Should()
            .BeEquivalentTo(new DashboardKpiResponse("inscriptions", 5, 5, 0));
        Kpi(r, "resources").Should().BeEquivalentTo(new DashboardKpiResponse("resources", 3, 2, 1));

        var memberSeries = Series(r.UserGrowth, "member");
        memberSeries[0].Should().Be(1);
        memberSeries[1].Should().Be(2);
        memberSeries[11].Should().Be(2);
        Series(r.UserGrowth, "sponsor")[2].Should().Be(1);
        Series(r.UserGrowth, "participant")[5].Should().Be(2);

        Series(r.Inscriptions, "confirmed")[1].Should().Be(2);
        Series(r.Inscriptions, "confirmed")[5].Should().Be(1);
        Series(r.Inscriptions, "requested")[2].Should().Be(1);

        Series(r.ContentPublished, "news")[1].Should().Be(1);
        Series(r.ContentPublished, "resources")[2].Should().Be(1);

        Slice(r.UsersByType, "member").Should().Be(2);
        Slice(r.UsersByType, "participant").Should().Be(2);
        Slice(r.AudienceComposition, "adults").Should().Be(4);
        Slice(r.AudienceComposition, "minors").Should().Be(1);
        Slice(r.ParticipantsByGender, "Male").Should().Be(1);
        Slice(r.ParticipantsByGender, "Other").Should().Be(1);
        Slice(r.ParticipantsByGender, "Female")
            .Should()
            .Be(0, "the two Female users are members, not participants");

        r.EventsByCategory.Select(c => c.Label).Should().Equal("Formación", "Robótica");
        r.EventsByCategory[0].Count.Should().Be(2);

        r.TopEvents.Select(t => t.Title).Should().Equal("Feria", "Taller");
        r.TopEvents[0].Confirmed.Should().Be(2);

        r.Occupancy.Confirmed.Should().Be(3);
        r.Occupancy.Desired.Should().Be(5);
        r.Occupancy.Events.Should().ContainSingle();
        var occEvent = r.Occupancy.Events[0];
        occEvent.EventId.Should().Be(occEventId);
        occEvent.Title.Should().Be("Robótica");
        occEvent.Confirmed.Should().Be(3);
        occEvent.Desired.Should().Be(5);
        occEvent.Activities.Should().ContainSingle();
        occEvent.Activities[0].Title.Should().Be("Taller de septiembre");
        occEvent.Activities[0].Confirmed.Should().Be(3);
        occEvent.Activities[0].Desired.Should().Be(5);
    }

    private void HasNoData()
    {
        users.HasUsers();
        activities.HasAssignments();
        events.HasEvents();
        HasActivityRows();
        HasResources();
        HasNews();
        HasPartners();
        HasCategoryTypes();
    }

    private Task<DashboardAnalyticsResponse> Analytics(DateOnly? from, DateOnly? to)
    {
        return sut.HandleAsync(
            new GetDashboardAnalyticsQuery(new DashboardAnalyticsQuery { From = from, To = to }),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncWithoutRangeCoversTheLastTwelveMonthsMonthly()
    {
        HasNoData();

        var r = await Analytics(null, null);

        r.RangeStart.Should().Be(new DateOnly(2025, 7, 7));
        r.RangeEnd.Should().Be(new DateOnly(2026, 7, 7));
        r.Granularity.Should().Be("month");
        r.UserGrowth.Buckets.Should().HaveCount(13);
        r.UserGrowth.Buckets[0].Should().Be(new DateOnly(2025, 7, 1));
        r.UserGrowth.Buckets[^1].Should().Be(new DateOnly(2026, 7, 1));
    }

    [Fact]
    public async Task HandleAsyncReversedShortRangeSwapsDatesAndUsesDailyBuckets()
    {
        HasNoData();

        var r = await Analytics(new DateOnly(2026, 6, 30), new DateOnly(2026, 6, 1));

        r.RangeStart.Should().Be(new DateOnly(2026, 6, 1));
        r.RangeEnd.Should().Be(new DateOnly(2026, 6, 30));
        r.Granularity.Should().Be("day");
        r.Inscriptions.Buckets.Should().HaveCount(30);
        r.Inscriptions.Buckets[1].Should().Be(new DateOnly(2026, 6, 2));
    }

    [Fact]
    public async Task HandleAsyncQuarterRangeUsesWeeklyBucketsStartingOnMonday()
    {
        HasNoData();

        var r = await Analytics(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));

        r.Granularity.Should().Be("week");
        r.ContentPublished.Buckets[0].Should().Be(new DateOnly(2025, 12, 29));
        r.ContentPublished.Buckets[1].Should().Be(new DateOnly(2026, 1, 5));
        r.ContentPublished.Buckets.Should().OnlyContain(b => b.DayOfWeek == DayOfWeek.Monday);
        r.ContentPublished.Buckets[^1].Should().Be(new DateOnly(2026, 3, 30));
    }

    [Fact]
    public async Task HandleAsyncUnknownUserTypeAndStatusAreLeftOutOfTheBreakdowns()
    {
        HasNoData();
        var unknown = Guid.NewGuid();
        users.HasUsers(
            AnalyticsUser(unknown, SeedIds.UserStatusTypes.Active, Utc(2026, 3, 1)),
            AnalyticsUser(SeedIds.UserTypes.Member, SeedIds.UserStatusTypes.Active, Utc(2026, 3, 1))
        );
        activities.HasAssignments(
            Insc(Guid.NewGuid(), unknown, Utc(2026, 3, 1)),
            Insc(Guid.NewGuid(), Confirmed, Utc(2026, 3, 1))
        );

        var r = await Analytics(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        r.UsersByType.Select(s => s.Key).Should().Equal("member", "sponsor", "participant");
        Slice(r.UsersByType, "member").Should().Be(1);
        r.UserGrowth.Series.Select(s => s.Key).Should().NotContain("other");
        r.Inscriptions.Series.Select(s => s.Key).Should().Equal("requested", "confirmed", "denied");
        Series(r.Inscriptions, "confirmed").Sum().Should().Be(1);
        Series(r.Inscriptions, "requested").Sum().Should().Be(0);
        Series(r.Inscriptions, "denied").Sum().Should().Be(0);
    }

    [Fact]
    public async Task HandleAsyncParticipantPreferringNotToSayGenderGetsItsOwnSlice()
    {
        HasNoData();
        users.HasUsers(
            AnalyticsUser(
                SeedIds.UserTypes.Participant,
                SeedIds.UserStatusTypes.Active,
                Utc(2026, 3, 1),
                gender: Gender.PreferNotToSay
            )
        );

        var r = await Analytics(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        r.ParticipantsByGender.Select(s => s.Key)
            .Should()
            .Equal("Male", "Female", "Other", "PreferNotToSay");
        Slice(r.ParticipantsByGender, "PreferNotToSay").Should().Be(1);
        Slice(r.ParticipantsByGender, "Other").Should().Be(0);
    }

    private static DateTimeOffset Utc(int year, int month, int day)
    {
        return new(year, month, day, 12, 0, 0, TimeSpan.Zero);
    }
}
