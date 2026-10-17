using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Application.Reports.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Reports.ReportTestData;
using Gender = CodigoActivo.Application.Abstractions.Querying.ReadModel.Gender;

namespace CodigoActivo.UnitTests.Application.Reports.Queries;

public sealed class GetDashboardAnalyticsQueryHandlerTests
{
    private static readonly Guid Leader = KnownIds.ActivityRoleTypes.Leader;
    private static readonly Guid Participant = KnownIds.ActivityRoleTypes.Participant;

    private readonly FakeReadStore store = new();
    private readonly TestClock clock = new(
        new DateTimeOffset(2026, 7, 7, 10, 0, 0, TimeSpan.Zero),
        new DateOnly(2026, 7, 7)
    );
    private readonly GetDashboardAnalyticsQueryHandler sut;

    public GetDashboardAnalyticsQueryHandlerTests()
    {
        sut = new GetDashboardAnalyticsQueryHandler(
            store,
            new FakeQueryExecutor(),
            new FakeDashboardCountsReader(store),
            clock
        );
    }

    private static UserRow AnalyticsUser(
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

    private static UserRow Person(DateOnly? birthDate = null, Gender gender = Gender.Female)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            FirstName = "P",
            LastName = "P",
            BirthDate = birthDate,
            ParentId = birthDate is null ? null : Guid.NewGuid(),
            Gender = gender,
        };
    }

    private static AssignmentRow Insc(
        Guid eventId,
        Guid statusId,
        DateTimeOffset createdAt,
        string eventTitle = "Evento"
    )
    {
        return new()
        {
            UserId = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            Activity = new ActivityRow
            {
                Description = "Descripción de la actividad",
                Location = "Sala principal",
                Title = "Actividad de prueba",
                EventId = eventId,
                Event = new EventRow { Id = eventId, Title = eventTitle },
            },
            AssignmentStatusId = statusId,
            CreatedAt = createdAt,
        };
    }

    private static ActivityRow AnalyticsActivity(
        DateTimeOffset startsAt,
        DateTimeOffset createdAt,
        int desired,
        int confirmed,
        Guid eventId,
        string eventTitle = "Evento",
        string title = "Actividad",
        int confirmedLeaders = 0
    )
    {
        var activity = new ActivityRow
        {
            Description = "Descripción de la actividad",
            Location = "Sala principal",
            Id = Guid.NewGuid(),
            Title = title,
            ActivityStartsAt = startsAt,
            CreatedAt = createdAt,
            EventId = eventId,
            Event = new EventRow
            {
                Subtitle = "Subtítulo del evento",
                Id = eventId,
                Title = eventTitle,
            },
        };
        if (desired > 0)
        {
            activity.RoleCapacities.Add(
                new RoleCapacityRow { DesiredCount = desired, ActivityRoleTypeId = Participant }
            );
        }

        for (var i = 0; i < confirmed; i++)
        {
            activity.Assignments.Add(
                new AssignmentRow
                {
                    AssignmentStatusId = Confirmed,
                    ActivityRoleTypeId = Participant,
                }
            );
        }

        for (var i = 0; i < confirmedLeaders; i++)
        {
            activity.Assignments.Add(
                new AssignmentRow { AssignmentStatusId = Confirmed, ActivityRoleTypeId = Leader }
            );
        }

        return activity;
    }

    private static EventCategoryTypeRow Category(string name, string color)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Color = color,
        };
    }

    private EventRow NewEvent(
        string title,
        DateOnly? endsAt = null,
        params EventCategoryTypeRow[] categories
    )
    {
        var ev = new EventRow
        {
            Id = Guid.NewGuid(),
            Title = title,
            Subtitle = "Subtítulo del evento",
            EventEndsAt = endsAt ?? default,
        };
        foreach (var category in categories)
        {
            ev.Categories.Add(
                new EventCategoryRow
                {
                    EventId = ev.Id,
                    Event = ev,
                    EventCategoryTypeId = category.Id,
                    EventCategoryType = category,
                }
            );
        }

        store.Events.Add(ev);
        return ev;
    }

    private ActivityRow AddActivity(
        EventRow ev,
        DateTimeOffset startsAt,
        params (Guid Role, int Desired)[] capacities
    )
    {
        var activity = new ActivityRow
        {
            Id = Guid.NewGuid(),
            Title = "Actividad",
            Description = "Descripción de la actividad",
            Location = "Sala principal",
            ActivityStartsAt = startsAt,
            EventId = ev.Id,
            Event = ev,
        };
        foreach (var (role, desired) in capacities)
        {
            activity.RoleCapacities.Add(
                new RoleCapacityRow
                {
                    ActivityId = activity.Id,
                    ActivityRoleTypeId = role,
                    DesiredCount = desired,
                }
            );
        }

        ev.Activities.Add(activity);
        store.Activities.Add(activity);
        return activity;
    }

    private void AddSignup(
        ActivityRow activity,
        UserRow person,
        Guid statusId,
        Guid? roleId = null,
        DateTimeOffset? createdAt = null
    )
    {
        var signup = new AssignmentRow
        {
            UserId = person.Id,
            User = person,
            ActivityId = activity.Id,
            Activity = activity,
            ActivityRoleTypeId = roleId ?? Participant,
            AssignmentStatusId = statusId,
            CreatedAt = createdAt ?? Utc(2026, 1, 1),
        };
        activity.Assignments.Add(signup);
        store.Assignments.Add(signup);
    }

    private void AddSignups(ActivityRow activity, Guid statusId, int count, Guid? roleId = null)
    {
        for (var i = 0; i < count; i++)
        {
            AddSignup(activity, Person(), statusId, roleId);
        }
    }

    private static void Rate(EventRow ev, params int?[] scores)
    {
        foreach (var score in scores)
        {
            ev.Ratings.Add(
                new EventRatingRow
                {
                    Id = Guid.NewGuid(),
                    EventId = ev.Id,
                    Event = ev,
                    Score = score,
                }
            );
        }
    }

    private static DashboardKpiResponse Kpi(DashboardAnalyticsResponse r, string key)
    {
        return r.Kpis.Single(k => string.Equals(k.Key, key, StringComparison.Ordinal));
    }

    private static IReadOnlyList<int> Values(
        IReadOnlyList<DashboardSeriesResponse> series,
        string key
    )
    {
        return series.Single(s => string.Equals(s.Key, key, StringComparison.Ordinal)).Values;
    }

    private Task<DashboardAnalyticsResponse> Analytics(DateOnly? from, DateOnly? to)
    {
        return sut.HandleAsync(
            new GetDashboardAnalyticsQuery(new DashboardAnalyticsQuery { From = from, To = to }),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncMixedDataProducesExpectedCountersAndSeries()
    {
        var e1 = new Guid("eeeeeeee-0000-0000-0000-000000000001");
        var e2 = new Guid("eeeeeeee-0000-0000-0000-000000000002");

        var member = KnownIds.UserTypes.Member;
        var sponsor = KnownIds.UserTypes.Sponsor;
        var participant = KnownIds.UserTypes.Participant;
        var active = KnownIds.UserStatusTypes.Active;
        var dependent = KnownIds.UserStatusTypes.Dependent;

        var parent = AnalyticsUser(participant, active, Utc(2026, 6, 20), gender: Gender.Other);
        store.Users.AddRange([
            AnalyticsUser(member, active, Utc(2025, 12, 1)),
            AnalyticsUser(member, active, Utc(2026, 2, 15)),
            AnalyticsUser(sponsor, active, Utc(2026, 3, 10), gender: Gender.Male),
            parent,
            AnalyticsUser(participant, dependent, Utc(2026, 6, 25), parent.Id, Gender.Male),
        ]);

        store.Assignments.AddRange([
            Insc(e1, Confirmed, Utc(2026, 2, 10), "Feria"),
            Insc(e1, Confirmed, Utc(2026, 2, 20), "Feria"),
            Insc(e1, Requested, Utc(2026, 3, 5), "Feria"),
            Insc(e2, Denied, Utc(2026, 6, 1), "Taller"),
            Insc(e2, Confirmed, Utc(2026, 6, 15), "Taller"),
        ]);

        store.Resources.AddRange([
            new ResourceRow
            {
                Subtitle = "Subtítulo del recurso",
                Title = "Recurso de prueba",
                CreatedAt = Utc(2026, 3, 1),
                ResourceTypeId = KnownIds.ResourceTypes.Internal,
            },
            new ResourceRow
            {
                Subtitle = "Subtítulo del recurso",
                Title = "Recurso de prueba",
                CreatedAt = Utc(2026, 4, 1),
                ResourceTypeId = KnownIds.ResourceTypes.External,
            },
            new ResourceRow
            {
                Subtitle = "Subtítulo del recurso",
                Title = "Recurso de prueba",
                CreatedAt = Utc(2025, 12, 15),
                ResourceTypeId = KnownIds.ResourceTypes.External,
            },
        ]);

        var occEventId = new Guid("cccccccc-0000-0000-0000-000000000001");
        store.Activities.AddRange([
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
            ),
        ]);

        var r = await Analytics(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        r.Granularity.Should().Be("month");
        r.NewUsers.Buckets.Should().HaveCount(12);

        Kpi(r, "users").Should().BeEquivalentTo(new DashboardKpiResponse("users", 5, 4, 1));
        Kpi(r, "members").Should().BeEquivalentTo(new DashboardKpiResponse("members", 2, 1, 1));
        Kpi(r, "inscriptions")
            .Should()
            .BeEquivalentTo(new DashboardKpiResponse("inscriptions", 5, 5, 0));
        Kpi(r, "resources").Should().BeEquivalentTo(new DashboardKpiResponse("resources", 3, 2, 1));
        Kpi(r, "activities").Total.Should().Be(2);

        Values(r.NewUsers.Series, "member")
            .Should()
            .Equal([0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], "the December member predates the range");
        Values(r.NewUsers.Series, "sponsor")[2].Should().Be(1);
        Values(r.NewUsers.Series, "participant")[5].Should().Be(2);

        Values(r.Inscriptions.Series, "confirmed")[1].Should().Be(2);
        Values(r.Inscriptions.Series, "confirmed")[5].Should().Be(1);
        Values(r.Inscriptions.Series, "requested")[2].Should().Be(1);

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

    [Fact]
    public async Task HandleAsyncOccupancyCountsEachRoleUpToItsDesiredSeats()
    {
        var eventId = new Guid("cccccccc-0000-0000-0000-000000000003");
        store.Activities.AddRange([
            AnalyticsActivity(
                Utc(2026, 9, 1),
                Utc(2026, 5, 1),
                desired: 2,
                confirmed: 4,
                eventId: eventId,
                title: "Muy solicitada",
                confirmedLeaders: 3
            ),
            AnalyticsActivity(
                Utc(2026, 9, 2),
                Utc(2026, 5, 1),
                desired: 4,
                confirmed: 1,
                eventId: eventId,
                title: "Con plazas libres"
            ),
        ]);

        var r = await Analytics(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        r.Occupancy.Confirmed.Should().Be(3);
        r.Occupancy.Desired.Should().Be(6);
        var occEvent = r.Occupancy.Events.Should().ContainSingle().Subject;
        occEvent.Confirmed.Should().Be(3);
        occEvent.Desired.Should().Be(6);
        occEvent.Activities.Select(a => (a.Confirmed, a.Desired)).Should().Equal((2, 2), (1, 4));
    }

    [Fact]
    public async Task HandleAsyncTopEventsRankConfirmedSignupsOfTheRangeWithTheirSeats()
    {
        var fair = NewEvent("Feria");
        var workshop = NewEvent("Taller");
        var fairMorning = AddActivity(fair, Utc(2026, 3, 1), (Participant, 2));
        var fairEvening = AddActivity(fair, Utc(2026, 3, 2), (Participant, 4));
        var workshopSession = AddActivity(workshop, Utc(2026, 4, 1));
        var formerSession = AddActivity(NewEvent("Antiguo"), Utc(2025, 3, 1), (Participant, 9));
        AddSignups(fairMorning, Confirmed, 3);
        AddSignups(fairEvening, Confirmed, 1);
        AddSignups(fairEvening, Requested, 2);
        AddSignups(workshopSession, Confirmed, 1);
        AddSignups(formerSession, Confirmed, 9);

        var r = await Analytics(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

        r.TopEvents.Should()
            .Equal(
                new DashboardTopEventResponse(fair.Id, "Feria", 4, 3, 6),
                new DashboardTopEventResponse(workshop.Id, "Taller", 1, 0, 0)
            );
    }

    [Fact]
    public async Task HandleAsyncPendingSignupsGroupRequestsOfUpcomingActivitiesByEvent()
    {
        var fair = NewEvent("Feria");
        var talk = NewEvent("Charla");
        var fairSoon = AddActivity(fair, Utc(2026, 7, 20));
        var fairLater = AddActivity(fair, Utc(2026, 8, 20));
        var talkSession = AddActivity(talk, Utc(2026, 7, 10));
        var finished = AddActivity(NewEvent("Terminado"), Utc(2026, 7, 1));
        AddSignup(fairLater, Person(), Requested, createdAt: Utc(2026, 6, 1));
        AddSignup(fairSoon, Person(), Requested, createdAt: Utc(2026, 6, 10));
        AddSignup(fairSoon, Person(), Confirmed, createdAt: Utc(2026, 5, 1));
        AddSignup(talkSession, Person(), Requested, createdAt: Utc(2026, 7, 1));
        AddSignup(finished, Person(), Requested, createdAt: Utc(2026, 5, 1));

        var r = await Analytics(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        r.PendingSignups.Total.Should().Be(3);
        r.PendingSignups.Events.Should()
            .Equal(
                new DashboardPendingEventResponse(
                    talk.Id,
                    "Charla",
                    1,
                    Utc(2026, 7, 1),
                    Utc(2026, 7, 10)
                ),
                new DashboardPendingEventResponse(
                    fair.Id,
                    "Feria",
                    2,
                    Utc(2026, 6, 1),
                    Utc(2026, 7, 20)
                )
            );
    }

    [Fact]
    public async Task HandleAsyncRoleCoverageSumsUpcomingSeatsPerRoleCappedPerActivity()
    {
        var camp = NewEvent("Campamento");
        var first = AddActivity(camp, Utc(2026, 8, 1), (Leader, 2), (Participant, 10));
        var second = AddActivity(camp, Utc(2026, 8, 2), (Leader, 1), (Participant, 5));
        var finished = AddActivity(camp, Utc(2026, 6, 1), (Leader, 3));
        AddSignups(first, Confirmed, 3, Leader);
        AddSignups(first, Confirmed, 4);
        AddSignups(second, Confirmed, 1, Leader);
        AddSignups(second, Requested, 2);
        AddSignups(finished, Confirmed, 3, Leader);

        var r = await Analytics(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        r.RoleCoverage.Should()
            .Equal(
                new DashboardRoleCoverageResponse("leader", 3, 3),
                new DashboardRoleCoverageResponse("volunteer", 0, 0),
                new DashboardRoleCoverageResponse("participant", 4, 15)
            );
    }

    [Fact]
    public async Task HandleAsyncParticipantProfileCountsDistinctConfirmedPeoplePerAgeGroupAndGender()
    {
        var ev = NewEvent("Jornada");
        var morning = AddActivity(ev, Utc(2026, 3, 1));
        var evening = AddActivity(ev, Utc(2026, 3, 1).AddHours(6));
        var outside = AddActivity(ev, Utc(2025, 3, 1));
        var adult = Person();
        AddSignup(morning, adult, Confirmed);
        AddSignup(evening, adult, Confirmed);
        AddSignup(morning, Person(new DateOnly(2022, 3, 1), Gender.Male), Confirmed);
        AddSignup(morning, Person(new DateOnly(2016, 1, 15)), Confirmed);
        AddSignup(morning, Person(new DateOnly(2010, 12, 1), Gender.PreferNotToSay), Confirmed);
        AddSignup(morning, Person(new DateOnly(2008, 6, 1), Gender.Other), Confirmed);
        AddSignup(evening, Person(gender: Gender.Male), Requested);
        AddSignup(outside, Person(gender: Gender.Male), Confirmed);

        var r = await Analytics(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

        var profile = r.ParticipantProfile;
        profile.Groups.Should().Equal("0-5", "6-11", "12-17", "adults");
        profile
            .Series.Select(s => s.Key)
            .Should()
            .Equal("Male", "Female", "Other", "PreferNotToSay");
        Values(profile.Series, "Male").Should().Equal(1, 0, 0, 0);
        Values(profile.Series, "Female").Should().Equal(0, 1, 0, 1);
        Values(profile.Series, "Other")
            .Should()
            .Equal([0, 0, 0, 1], "a dependent who turned 18 by the end of the range is an adult");
        Values(profile.Series, "PreferNotToSay").Should().Equal(0, 0, 1, 0);
    }

    [Fact]
    public async Task HandleAsyncCategoryDemandCountsConfirmedSignupsInEveryCategoryOfTheirEvent()
    {
        var training = Category("Formación", "#F97316");
        var robotics = Category("Robótica", "#84CC16");
        var art = Category("Arte", "#0EA5E9");
        var robotsInClass = NewEvent("Robots en clase", null, training, robotics);
        var course = NewEvent("Curso", null, training);
        var exhibition = NewEvent("Exposición", null, art);
        AddSignups(AddActivity(robotsInClass, Utc(2026, 3, 1)), Confirmed, 2);
        AddSignups(AddActivity(course, Utc(2026, 4, 1)), Confirmed, 1);
        AddSignups(AddActivity(course, Utc(2026, 4, 2)), Requested, 5);
        AddSignups(AddActivity(exhibition, Utc(2025, 4, 1)), Confirmed, 4);

        var r = await Analytics(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

        r.CategoryDemand.Should()
            .Equal(
                new DashboardSliceResponse(training.Id.ToString(), "Formación", "#F97316", 3),
                new DashboardSliceResponse(robotics.Id.ToString(), "Robótica", "#84CC16", 2)
            );
    }

    [Fact]
    public async Task HandleAsyncRecurrenceGroupsPeopleByTheDistinctEventsTheyJoined()
    {
        var sessions = Enumerable
            .Range(0, 6)
            .Select(i => AddActivity(NewEvent($"Evento {i}"), Utc(2026, 2, 1 + i)))
            .ToList();
        var once = Person();
        AddSignup(sessions[0], once, Confirmed);
        AddSignup(AddActivity(sessions[0].Event, Utc(2026, 3, 1)), once, Confirmed);
        var twice = Person();
        sessions.Take(2).ToList().ForEach(session => AddSignup(session, twice, Confirmed));
        var thrice = Person();
        sessions.Take(3).ToList().ForEach(session => AddSignup(session, thrice, Confirmed));
        var regular = Person();
        sessions.ForEach(session => AddSignup(session, regular, Confirmed));
        AddSignup(sessions[1], Person(), Requested);

        var r = await Analytics(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

        r.Recurrence.Select(s => (s.Key, s.Count))
            .Should()
            .Equal(("1", 1), ("2", 1), ("3-5", 1), ("6+", 1));
    }

    [Fact]
    public async Task HandleAsyncSatisfactionAveragesTheScoresOfEventsEndingInTheRange()
    {
        var spring = NewEvent("Primavera", new DateOnly(2026, 3, 10));
        var may = NewEvent("Mayo", new DateOnly(2026, 5, 20));
        Rate(spring, 5, 4, null);
        Rate(may, 3);
        Rate(NewEvent("Sin puntuación", new DateOnly(2026, 6, 1)), [null]);
        Rate(NewEvent("Anterior", new DateOnly(2025, 12, 31)), 1);

        var r = await Analytics(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

        r.Satisfaction.Average.Should().Be(4.0);
        r.Satisfaction.Scored.Should().Be(3);
        r.Satisfaction.Events.Should()
            .Equal(
                new DashboardEventSatisfactionResponse(may.Id, "Mayo", 3.0, 1),
                new DashboardEventSatisfactionResponse(spring.Id, "Primavera", 4.5, 2)
            );
    }

    [Fact]
    public async Task HandleAsyncWithoutRatingsHasNoAverage()
    {
        var r = await Analytics(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

        r.Satisfaction.Should().BeEquivalentTo(new DashboardSatisfactionResponse(null, 0, []));
        r.Recurrence.Should().OnlyContain(slice => slice.Count == 0);
        r.CategoryDemand.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncWithoutRangeCoversTheLastTwelveMonthsMonthly()
    {
        var r = await Analytics(null, null);

        r.RangeStart.Should().Be(new DateOnly(2025, 7, 7));
        r.RangeEnd.Should().Be(new DateOnly(2026, 7, 7));
        r.Granularity.Should().Be("month");
        r.NewUsers.Buckets.Should().HaveCount(13);
        r.NewUsers.Buckets[0].Should().Be(new DateOnly(2025, 7, 1));
        r.NewUsers.Buckets[^1].Should().Be(new DateOnly(2026, 7, 1));
    }

    [Fact]
    public async Task HandleAsyncReversedShortRangeSwapsDatesAndUsesDailyBuckets()
    {
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
        var r = await Analytics(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));

        r.Granularity.Should().Be("week");
        r.NewUsers.Buckets[0].Should().Be(new DateOnly(2025, 12, 29));
        r.NewUsers.Buckets[1].Should().Be(new DateOnly(2026, 1, 5));
        r.NewUsers.Buckets.Should().OnlyContain(b => b.DayOfWeek == DayOfWeek.Monday);
        r.NewUsers.Buckets[^1].Should().Be(new DateOnly(2026, 3, 30));
    }

    [Fact]
    public async Task HandleAsyncLocalTimeZoneCountsByLocalDayAcrossTheSummerTimeChange()
    {
        clock.TimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "CET-test",
            TimeSpan.FromHours(1),
            "CET-test",
            "CET-test",
            "CEST-test",
            [
                TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                    new DateTime(2000, 1, 1),
                    new DateTime(2099, 12, 31),
                    TimeSpan.FromHours(1),
                    TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
                        new DateTime(1, 1, 1, 2, 0, 0),
                        3,
                        5,
                        DayOfWeek.Sunday
                    ),
                    TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
                        new DateTime(1, 1, 1, 3, 0, 0),
                        10,
                        5,
                        DayOfWeek.Sunday
                    )
                ),
            ]
        );
        store.Users.Add(
            AnalyticsUser(
                KnownIds.UserTypes.Member,
                KnownIds.UserStatusTypes.Active,
                new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero)
            )
        );
        store.News.Add(
            new NewsItemRow
            {
                Subtitle = "Subtítulo de la noticia",
                Title = "Pasada la medianoche local",
                CreatedAt = new DateTimeOffset(2026, 3, 8, 23, 30, 0, TimeSpan.Zero),
            }
        );
        store.Assignments.AddRange([
            Insc(
                Guid.NewGuid(),
                Confirmed,
                new DateTimeOffset(2026, 3, 8, 23, 30, 0, TimeSpan.Zero)
            ),
            Insc(
                Guid.NewGuid(),
                Requested,
                new DateTimeOffset(2026, 4, 5, 22, 30, 0, TimeSpan.Zero)
            ),
        ]);

        var r = await Analytics(new DateOnly(2026, 3, 4), new DateOnly(2026, 4, 29));

        r.Granularity.Should().Be("week");
        r.Inscriptions.Buckets[0].Should().Be(new DateOnly(2026, 3, 2));
        Values(r.Inscriptions.Series, "confirmed").Should().Equal(0, 1, 0, 0, 0, 0, 0, 0, 0);
        Values(r.Inscriptions.Series, "requested").Should().Equal(0, 0, 0, 0, 0, 1, 0, 0, 0);
        Values(r.NewUsers.Series, "member").Should().OnlyContain(value => value == 0);
        Kpi(r, "users").Should().Be(new DashboardKpiResponse("users", 1, 0, 1));
        Kpi(r, "news").Should().Be(new DashboardKpiResponse("news", 1, 1, 0));
    }

    [Fact]
    public async Task HandleAsyncUnknownUserTypeAndStatusAreLeftOutOfTheSeries()
    {
        var unknown = Guid.NewGuid();
        store.Users.AddRange([
            AnalyticsUser(unknown, KnownIds.UserStatusTypes.Active, Utc(2026, 3, 1)),
            AnalyticsUser(
                KnownIds.UserTypes.Member,
                KnownIds.UserStatusTypes.Active,
                Utc(2026, 3, 1)
            ),
        ]);
        store.Assignments.AddRange([
            Insc(Guid.NewGuid(), unknown, Utc(2026, 3, 1)),
            Insc(Guid.NewGuid(), Confirmed, Utc(2026, 3, 1)),
        ]);

        var r = await Analytics(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        r.NewUsers.Series.Select(s => s.Key).Should().Equal("member", "sponsor", "participant");
        Values(r.NewUsers.Series, "member").Sum().Should().Be(1);
        r.Inscriptions.Series.Select(s => s.Key).Should().Equal("requested", "confirmed", "denied");
        Values(r.Inscriptions.Series, "confirmed").Sum().Should().Be(1);
        Values(r.Inscriptions.Series, "requested").Sum().Should().Be(0);
        Values(r.Inscriptions.Series, "denied").Sum().Should().Be(0);
    }

    private static DateTimeOffset Utc(int year, int month, int day)
    {
        return new(year, month, day, 12, 0, 0, TimeSpan.Zero);
    }
}
