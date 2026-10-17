using System.Linq.Expressions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Reports.Queries;

/// <summary>
/// Carries the criteria used to retrieve dashboard analytics.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record GetDashboardAnalyticsQuery(DashboardAnalyticsQuery Filters)
    : IQuery<DashboardAnalyticsResponse>,
        ICachedQuery
{
    /// <inheritdoc />
    public CacheDuration Duration => CacheDuration.Dashboard;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Tags => CacheTags.DashboardAnalyticsSources;

    /// <inheritdoc />
    public string CacheKey(DateOnly today)
    {
        var (start, end, granularity) = Window(today);
        return $"reports:dashboard:analytics:{start:yyyy-MM-dd}:{end:yyyy-MM-dd}:{granularity}";
    }

    /// <summary>
    /// Resolves the days the analytics cover, the twelve months up to the end when no start is
    /// given, and the bucket size that keeps the charts readable.
    /// </summary>
    /// <param name="today">Current day, the end when none is given.</param>
    /// <returns>The first and last day, in order, and the granularity.</returns>
    public (DateOnly Start, DateOnly End, string Granularity) Window(DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(Filters);
        var end = Filters.To ?? today;
        var start = Filters.From ?? end.AddMonths(-12);
        if (start > end)
        {
            (start, end) = (end, start);
        }

        var totalDays = end.DayNumber - start.DayNumber + 1;
        var granularity = totalDays switch
        {
            <= 45 => "day",
            <= 182 => "week",
            _ => "month",
        };
        return (start, end, granularity);
    }
}

/// <summary>
/// Executes the query to retrieve dashboard analytics. Every section reads only what it shows:
/// creation times are counted per bucket in the database, the breakdowns are grouped there, and
/// only the activities of the range and the upcoming ones are loaded to compute their seats.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="countsReader">Read port that counts creation times per bucket.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class GetDashboardAnalyticsQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    IDashboardCountsReader countsReader,
    IClock clock
) : IQueryHandler<GetDashboardAnalyticsQuery, DashboardAnalyticsResponse>
{
    private const int TopEventCount = 8;

    private const int SatisfactionEventCount = 8;

    private static readonly string[] UserTypeKeys = ["member", "sponsor", "participant"];

    private static readonly string[] InscriptionKeys = ["requested", "confirmed", "denied"];

    private static readonly string[] GenderKeys = ["Male", "Female", "Other", "PreferNotToSay"];

    private static readonly string[] AgeGroupKeys = ["0-5", "6-11", "12-17", "adults"];

    private static readonly string[] RecurrenceKeys = ["1", "2", "3-5", "6+"];

    private static readonly (string Key, Guid Id)[] RoleIds =
    [
        ("leader", CatalogIds.ActivityRoles.IdOf(ActivityRole.Leader)),
        ("volunteer", CatalogIds.ActivityRoles.IdOf(ActivityRole.Volunteer)),
        ("participant", CatalogIds.ActivityRoles.IdOf(ActivityRole.Participant)),
    ];

    private static readonly Guid RequestedId = CatalogIds.AssignmentStatuses.IdOf(
        AssignmentStatus.Requested
    );

    private static readonly Guid DeniedId = CatalogIds.AssignmentStatuses.IdOf(
        AssignmentStatus.Denied
    );

    private static readonly Guid MemberId = CatalogIds.UserTypes.IdOf(UserType.Member);

    private static readonly Guid SponsorId = CatalogIds.UserTypes.IdOf(UserType.Sponsor);

    private static readonly Guid ParticipantId = CatalogIds.UserTypes.IdOf(UserType.Participant);

    /// <summary>
    /// Handles the request to retrieve dashboard analytics.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a dashboard analytics.</returns>
    public async Task<DashboardAnalyticsResponse> HandleAsync(
        GetDashboardAnalyticsQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var (start, end, granularity) = query.Window(clock.Today);
        var timeline = DashboardTimeline.Create(start, end, granularity, clock.TimeZone);
        var created = await countsReader.CountCreatedAsync(timeline.Thresholds, ct);
        var from = LocalDayRange.LowerUtc(start, clock.TimeZone);
        var until = LocalDayRange.UpperExclusiveUtc(end, clock.TimeZone);
        var now = clock.UtcNow;
        var inRange = await SeatsAsync(
            a => a.ActivityStartsAt >= from && a.ActivityStartsAt < until,
            ct
        );
        var upcoming = await SeatsAsync(a => a.ActivityStartsAt >= now, ct);

        return new DashboardAnalyticsResponse(
            start,
            end,
            granularity,
            Kpis(timeline, created),
            NewUsers(timeline, created),
            Inscriptions(timeline, created),
            TopEvents(inRange),
            await PendingSignupsAsync(now, ct),
            RoleCoverage(upcoming),
            await ParticipantProfileAsync(from, until, end, ct),
            await CategoryDemandAsync(from, until, ct),
            await RecurrenceAsync(from, until, ct),
            await SatisfactionAsync(start, end, ct),
            Occupancy(upcoming)
        );
    }

    private static List<DashboardKpiResponse> Kpis(
        DashboardTimeline timeline,
        DashboardCreatedCounts created
    )
    {
        return
        [
            timeline.Kpi("users", Unkeyed(created.Users)),
            timeline.Kpi("members", Unkeyed(created.Users.Where(count => count.Key == MemberId))),
            timeline.Kpi("inscriptions", Unkeyed(created.Inscriptions)),
            timeline.Kpi("events", created.Events),
            timeline.Kpi("activities", created.Activities),
            timeline.Kpi("resources", created.Resources),
            timeline.Kpi("news", created.News),
            timeline.Kpi("partners", created.Partners),
        ];
    }

    private static DashboardTimeSeriesResponse NewUsers(
        DashboardTimeline timeline,
        DashboardCreatedCounts created
    )
    {
        var byKey = created.Users.ToLookup(count => UserTypeKey(count.Key), StringComparer.Ordinal);
        return new DashboardTimeSeriesResponse(
            timeline.Buckets,
            [.. UserTypeKeys.Select(key => timeline.Flow(key, Unkeyed(byKey[key])))]
        );
    }

    private static DashboardTimeSeriesResponse Inscriptions(
        DashboardTimeline timeline,
        DashboardCreatedCounts created
    )
    {
        var byKey = created.Inscriptions.ToLookup(
            count => AssignmentStatusKey(count.Key),
            StringComparer.Ordinal
        );
        return new DashboardTimeSeriesResponse(
            timeline.Buckets,
            [.. InscriptionKeys.Select(key => timeline.Flow(key, Unkeyed(byKey[key])))]
        );
    }

    private IQueryable<AssignmentRow> ConfirmedIn(DateTimeOffset from, DateTimeOffset until)
    {
        return readStore
            .Assignments.Where(ActivityProjections.ConfirmedSignup)
            .Where(a => a.Activity.ActivityStartsAt >= from && a.Activity.ActivityStartsAt < until);
    }

    private async Task<IReadOnlyList<ActivitySeats>> SeatsAsync(
        Expression<Func<ActivityRow, bool>> filter,
        CancellationToken ct
    )
    {
        return await executor.ToListAsync(
            readStore
                .Activities.Where(filter)
                .Select(a => new ActivitySeats
                {
                    Id = a.Id,
                    Title = a.Title,
                    StartsAt = a.ActivityStartsAt,
                    EventId = a.EventId,
                    EventTitle = a.Event.Title,
                    Confirmed = a.Assignments.Count(x =>
                        x.AssignmentStatusId == ActivityProjections.ConfirmedStatusId
                    ),
                    Roles = a
                        .RoleCapacities.Select(c => new RoleSeats
                        {
                            RoleTypeId = c.ActivityRoleTypeId,
                            Desired = c.DesiredCount,
                            Confirmed = a.Assignments.Count(x =>
                                x.ActivityRoleTypeId == c.ActivityRoleTypeId
                                && x.AssignmentStatusId == ActivityProjections.ConfirmedStatusId
                            ),
                        })
                        .ToList(),
                }),
            ct
        );
    }

    private static List<DashboardTopEventResponse> TopEvents(IReadOnlyList<ActivitySeats> inRange)
    {
        return
        [
            .. inRange
                .GroupBy(a => (a.EventId, a.EventTitle))
                .Select(g => new DashboardTopEventResponse(
                    g.Key.EventId,
                    g.Key.EventTitle,
                    g.Sum(a => a.Confirmed),
                    g.Sum(a => a.Filled),
                    g.Sum(a => a.Desired)
                ))
                .Where(e => e.Confirmed > 0)
                .OrderByDescending(e => e.Confirmed)
                .ThenBy(e => e.Title, StringComparer.Ordinal)
                .ThenBy(e => e.EventId)
                .Take(TopEventCount),
        ];
    }

    private async Task<DashboardPendingSignupsResponse> PendingSignupsAsync(
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var pending = await executor.ToListAsync(
            readStore
                .Assignments.Where(a =>
                    a.AssignmentStatusId == RequestedId && a.Activity.ActivityStartsAt >= now
                )
                .Select(a => new
                {
                    a.Activity.EventId,
                    a.Activity.Event.Title,
                    a.CreatedAt,
                    a.Activity.ActivityStartsAt,
                })
                .GroupBy(a => new { a.EventId, a.Title })
                .Select(g => new
                {
                    g.Key.EventId,
                    g.Key.Title,
                    Pending = g.Count(),
                    OldestRequestedAt = g.Min(a => a.CreatedAt),
                    NextActivityStartsAt = g.Min(a => a.ActivityStartsAt),
                }),
            ct
        );

        return new DashboardPendingSignupsResponse(
            pending.Sum(e => e.Pending),
            [
                .. pending
                    .OrderBy(e => e.NextActivityStartsAt)
                    .ThenBy(e => e.Title, StringComparer.Ordinal)
                    .ThenBy(e => e.EventId)
                    .Select(e => new DashboardPendingEventResponse(
                        e.EventId,
                        e.Title,
                        e.Pending,
                        e.OldestRequestedAt,
                        e.NextActivityStartsAt
                    )),
            ]
        );
    }

    private static List<DashboardRoleCoverageResponse> RoleCoverage(
        IReadOnlyList<ActivitySeats> upcoming
    )
    {
        var seats = upcoming.SelectMany(a => a.Roles).ToList();
        return
        [
            .. RoleIds.Select(role =>
            {
                var ofRole = seats.Where(s => s.RoleTypeId == role.Id).ToList();
                return new DashboardRoleCoverageResponse(
                    role.Key,
                    ofRole.Sum(s => s.Filled),
                    ofRole.Sum(s => s.Desired)
                );
            }),
        ];
    }

    private async Task<DashboardBreakdownResponse> ParticipantProfileAsync(
        DateTimeOffset from,
        DateTimeOffset until,
        DateOnly end,
        CancellationToken ct
    )
    {
        var people = await executor.ToListAsync(
            ConfirmedIn(from, until)
                .Select(a => new
                {
                    a.UserId,
                    a.User.BirthDate,
                    a.User.Gender,
                })
                .Distinct()
                .GroupBy(p => new { p.BirthDate, p.Gender })
                .Select(g => new
                {
                    g.Key.BirthDate,
                    g.Key.Gender,
                    Count = g.Count(),
                }),
            ct
        );
        var counts = people
            .GroupBy(p => (Group: AgeGroupKey(p.BirthDate, end), Gender: p.Gender.ToString()))
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Count));

        return new DashboardBreakdownResponse(
            AgeGroupKeys,
            [
                .. GenderKeys.Select(gender => new DashboardSeriesResponse(
                    gender,
                    [.. AgeGroupKeys.Select(group => counts.GetValueOrDefault((group, gender)))]
                )),
            ]
        );
    }

    private async Task<List<DashboardSliceResponse>> CategoryDemandAsync(
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken ct
    )
    {
        var categories = await executor.ToListAsync(
            ConfirmedIn(from, until)
                .SelectMany(a => a.Activity.Event.Categories)
                .GroupBy(c => new
                {
                    c.EventCategoryTypeId,
                    c.EventCategoryType.Name,
                    c.EventCategoryType.Color,
                })
                .Select(g => new
                {
                    g.Key.EventCategoryTypeId,
                    g.Key.Name,
                    g.Key.Color,
                    Count = g.Count(),
                }),
            ct
        );
        return
        [
            .. categories
                .OrderByDescending(c => c.Count)
                .ThenBy(c => TextSearch.Normalize(c.Name), StringComparer.Ordinal)
                .ThenBy(c => c.EventCategoryTypeId)
                .Select(c => new DashboardSliceResponse(
                    c.EventCategoryTypeId.ToString(),
                    c.Name,
                    c.Color,
                    c.Count
                )),
        ];
    }

    private async Task<List<DashboardSliceResponse>> RecurrenceAsync(
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken ct
    )
    {
        var eventsPerPerson = await executor.ToListAsync(
            ConfirmedIn(from, until)
                .Select(a => new { a.UserId, a.Activity.EventId })
                .Distinct()
                .GroupBy(p => p.UserId)
                .Select(g => g.Count()),
            ct
        );
        return FixedSlices(
            RecurrenceKeys,
            eventsPerPerson
                .GroupBy(RecurrenceKey, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal)
        );
    }

    private async Task<DashboardSatisfactionResponse> SatisfactionAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken ct
    )
    {
        var events = await executor.ToListAsync(
            readStore
                .Events.Where(e => e.EventEndsAt >= start && e.EventEndsAt <= end)
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    e.EventEndsAt,
                    Scored = e.Ratings.Count(r => r.Score != null),
                    ScoreSum = e.Ratings.Sum(r => r.Score) ?? 0,
                })
                .Where(e => e.Scored > 0),
            ct
        );
        var scored = events.Sum(e => e.Scored);

        return new DashboardSatisfactionResponse(
            scored > 0 ? (double)events.Sum(e => e.ScoreSum) / scored : null,
            scored,
            [
                .. events
                    .OrderByDescending(e => e.EventEndsAt)
                    .ThenBy(e => e.Title, StringComparer.Ordinal)
                    .ThenBy(e => e.Id)
                    .Take(SatisfactionEventCount)
                    .Select(e => new DashboardEventSatisfactionResponse(
                        e.Id,
                        e.Title,
                        (double)e.ScoreSum / e.Scored,
                        e.Scored
                    )),
            ]
        );
    }

    private static DashboardOccupancyResponse Occupancy(IReadOnlyList<ActivitySeats> upcoming)
    {
        var withCapacity = upcoming.Where(a => a.Desired > 0).ToList();
        var events = withCapacity
            .GroupBy(a => (a.EventId, a.EventTitle))
            .Select(g => new
            {
                g.Key.EventId,
                g.Key.EventTitle,
                MinStart = g.Min(a => a.StartsAt),
                Activities = g.OrderBy(a => a.StartsAt)
                    .ThenBy(a => a.Title, StringComparer.Ordinal)
                    .Select(a => new DashboardOccupancyActivityResponse(
                        a.Id,
                        a.Title,
                        a.StartsAt,
                        a.Filled,
                        a.Desired
                    ))
                    .ToList(),
            })
            .OrderBy(e => e.MinStart)
            .ThenBy(e => e.EventTitle, StringComparer.Ordinal)
            .Select(e => new DashboardOccupancyEventResponse(
                e.EventId,
                e.EventTitle,
                e.Activities.Sum(a => a.Confirmed),
                e.Activities.Sum(a => a.Desired),
                e.Activities
            ))
            .ToList();
        return new DashboardOccupancyResponse(
            withCapacity.Sum(a => a.Filled),
            withCapacity.Sum(a => a.Desired),
            events
        );
    }

    private static IEnumerable<IntervalCount> Unkeyed(IEnumerable<KeyedIntervalCount> counts)
    {
        return counts.Select(count => new IntervalCount(count.Interval, count.Count));
    }

    private static List<DashboardSliceResponse> FixedSlices(
        IReadOnlyList<string> keys,
        IReadOnlyDictionary<string, int> counts
    )
    {
        return
        [
            .. keys.Select(k => new DashboardSliceResponse(
                k,
                null,
                null,
                counts.GetValueOrDefault(k)
            )),
        ];
    }

    private static string AgeGroupKey(DateOnly? birthDate, DateOnly day)
    {
        if (birthDate is not { } born || !born.IsMinor(day))
        {
            return "adults";
        }

        return born.AgeOn(day) switch
        {
            < 6 => "0-5",
            < 12 => "6-11",
            _ => "12-17",
        };
    }

    private static string RecurrenceKey(int events)
    {
        return events switch
        {
            1 => "1",
            2 => "2",
            <= 5 => "3-5",
            _ => "6+",
        };
    }

    private static string UserTypeKey(Guid id)
    {
        return id switch
        {
            _ when id == MemberId => "member",
            _ when id == SponsorId => "sponsor",
            _ when id == ParticipantId => "participant",
            _ => "other",
        };
    }

    private static string AssignmentStatusKey(Guid id)
    {
        return id switch
        {
            _ when id == RequestedId => "requested",
            _ when id == ActivityProjections.ConfirmedStatusId => "confirmed",
            _ when id == DeniedId => "denied",
            _ => "other",
        };
    }

    private sealed class ActivitySeats
    {
        public Guid Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public DateTimeOffset StartsAt { get; init; }

        public Guid EventId { get; init; }

        public string EventTitle { get; init; } = string.Empty;

        public int Confirmed { get; init; }

        public List<RoleSeats> Roles { get; init; } = [];

        public int Desired => Roles.Sum(role => role.Desired);

        public int Filled => Roles.Sum(role => role.Filled);
    }

    private sealed class RoleSeats
    {
        public Guid RoleTypeId { get; init; }

        public int Desired { get; init; }

        public int Confirmed { get; init; }

        public int Filled => Math.Min(Confirmed, Desired);
    }
}
