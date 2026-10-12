using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Users;
using Gender = CodigoActivo.Application.Abstractions.Querying.ReadModel.Gender;

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
/// only the upcoming activities are loaded to compute their occupancy.
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

    private static readonly string[] UserGrowthKeys = ["member", "sponsor", "participant"];

    private static readonly string[] InscriptionKeys = ["requested", "confirmed", "denied"];

    private static readonly string[] GenderKeys = ["Male", "Female", "Other", "PreferNotToSay"];

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
        var people = await CountPeopleAsync(ct);

        return new DashboardAnalyticsResponse(
            start,
            end,
            granularity,
            Kpis(timeline, created),
            UserGrowth(timeline, created),
            Inscriptions(timeline, created),
            ContentPublished(timeline, created),
            UsersByType(created),
            Audience(people),
            ParticipantsByGender(people),
            await EventsByCategoryAsync(ct),
            await TopEventsAsync(ct),
            await EventsCalendarAsync(ct),
            await OccupancyAsync(ct)
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

    private static DashboardTimeSeriesResponse UserGrowth(
        DashboardTimeline timeline,
        DashboardCreatedCounts created
    )
    {
        var byKey = created.Users.ToLookup(count => UserTypeKey(count.Key), StringComparer.Ordinal);
        return new DashboardTimeSeriesResponse(
            timeline.Buckets,
            [.. UserGrowthKeys.Select(key => timeline.Cumulative(key, Unkeyed(byKey[key])))]
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

    private static DashboardTimeSeriesResponse ContentPublished(
        DashboardTimeline timeline,
        DashboardCreatedCounts created
    )
    {
        return new DashboardTimeSeriesResponse(
            timeline.Buckets,
            [timeline.Flow("news", created.News), timeline.Flow("resources", created.Resources)]
        );
    }

    private async Task<IReadOnlyList<PeopleCount>> CountPeopleAsync(CancellationToken ct)
    {
        return await executor.ToListAsync(
            readStore
                .Users.GroupBy(user => new
                {
                    user.UserTypeId,
                    user.Gender,
                    IsMinor = user.ParentId != null,
                })
                .Select(group => new PeopleCount(
                    group.Key.UserTypeId,
                    group.Key.Gender,
                    group.Key.IsMinor,
                    group.Count()
                )),
            ct
        );
    }

    private static List<DashboardSliceResponse> UsersByType(DashboardCreatedCounts created)
    {
        return FixedSlices(
            UserGrowthKeys,
            created
                .Users.GroupBy(count => UserTypeKey(count.Key), StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Sum(count => count.Count), StringComparer.Ordinal)
        );
    }

    private static List<DashboardSliceResponse> Audience(IReadOnlyList<PeopleCount> people)
    {
        return FixedSlices(
            ["adults", "minors"],
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["adults"] = people.Where(group => !group.IsMinor).Sum(group => group.Count),
                ["minors"] = people.Where(group => group.IsMinor).Sum(group => group.Count),
            }
        );
    }

    private static List<DashboardSliceResponse> ParticipantsByGender(
        IReadOnlyList<PeopleCount> people
    )
    {
        return FixedSlices(
            GenderKeys,
            people
                .Where(group => group.UserTypeId == ParticipantId)
                .GroupBy(group => group.Gender.ToString(), StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Sum(group => group.Count), StringComparer.Ordinal)
        );
    }

    private async Task<List<DashboardSliceResponse>> EventsByCategoryAsync(CancellationToken ct)
    {
        var categories = await executor.ToListAsync(
            readStore
                .EventCategoryTypes.Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Color,
                    t.Events.Count,
                })
                .Where(c => c.Count > 0),
            ct
        );
        return
        [
            .. categories
                .OrderByDescending(c => c.Count)
                .ThenBy(c => TextSearch.Normalize(c.Name), StringComparer.Ordinal)
                .ThenBy(c => c.Id)
                .Select(c => new DashboardSliceResponse(c.Id.ToString(), c.Name, c.Color, c.Count)),
        ];
    }

    private async Task<List<DashboardTopEventResponse>> TopEventsAsync(CancellationToken ct)
    {
        var confirmed = await executor.ToListAsync(
            readStore
                .Assignments.Where(ActivityProjections.ConfirmedSignup)
                .GroupBy(a => new { a.Activity.EventId, a.Activity.Event.Title })
                .Select(g => new
                {
                    g.Key.EventId,
                    g.Key.Title,
                    Confirmed = g.Count(),
                }),
            ct
        );

        return
        [
            .. confirmed
                .OrderByDescending(e => e.Confirmed)
                .ThenBy(e => e.Title, StringComparer.Ordinal)
                .ThenBy(e => e.EventId)
                .Take(TopEventCount)
                .Select(e => new DashboardTopEventResponse(e.EventId, e.Title, e.Confirmed)),
        ];
    }

    private async Task<DashboardTimeSeriesResponse> EventsCalendarAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-6);
        var months = DashboardTimeline.BucketsBetween(first, first.AddMonths(12), "month");
        var afterLast = first.AddMonths(months.Count);
        var counted = await executor.ToListAsync(
            readStore
                .Events.Where(e => e.EventStartsAt >= first && e.EventStartsAt < afterLast)
                .GroupBy(e => new { e.EventStartsAt.Year, e.EventStartsAt.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Past = g.Count(e => e.EventStartsAt < today),
                    All = g.Count(),
                }),
            ct
        );
        var byMonth = counted.ToDictionary(m => new DateOnly(m.Year, m.Month, 1));

        return new DashboardTimeSeriesResponse(
            months,
            [
                new DashboardSeriesResponse(
                    "past",
                    [.. months.Select(month => byMonth.TryGetValue(month, out var m) ? m.Past : 0)]
                ),
                new DashboardSeriesResponse(
                    "upcoming",
                    [
                        .. months.Select(month =>
                            byMonth.TryGetValue(month, out var m) ? m.All - m.Past : 0
                        ),
                    ]
                ),
            ]
        );
    }

    private async Task<DashboardOccupancyResponse> OccupancyAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var upcoming = await executor.ToListAsync(
            readStore
                .Activities.Where(a => a.ActivityStartsAt >= now)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.ActivityStartsAt,
                    a.EventId,
                    EventTitle = a.Event.Title,
                    Roles = a
                        .RoleCapacities.Select(c => new
                        {
                            c.DesiredCount,
                            Confirmed = a.Assignments.Count(x =>
                                x.ActivityRoleTypeId == c.ActivityRoleTypeId
                                && x.AssignmentStatusId == ActivityProjections.ConfirmedStatusId
                            ),
                        })
                        .ToList(),
                }),
            ct
        );
        var withCapacity = upcoming
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.ActivityStartsAt,
                a.EventId,
                a.EventTitle,
                Desired = a.Roles.Sum(role => role.DesiredCount),
                Confirmed = a.Roles.Sum(role => Math.Min(role.Confirmed, role.DesiredCount)),
            })
            .Where(a => a.Desired > 0)
            .ToList();
        var events = withCapacity
            .GroupBy(a => new { a.EventId, a.EventTitle })
            .Select(g => new
            {
                g.Key.EventId,
                g.Key.EventTitle,
                MinStart = g.Min(a => a.ActivityStartsAt),
                Activities = g.OrderBy(a => a.ActivityStartsAt)
                    .ThenBy(a => a.Title, StringComparer.Ordinal)
                    .Select(a => new DashboardOccupancyActivityResponse(
                        a.Id,
                        a.Title,
                        a.ActivityStartsAt,
                        a.Confirmed,
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
            withCapacity.Sum(a => a.Confirmed),
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

    private sealed record PeopleCount(Guid UserTypeId, Gender Gender, bool IsMinor, int Count);
}
