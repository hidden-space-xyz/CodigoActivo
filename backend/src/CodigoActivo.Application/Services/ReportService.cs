using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Services;

public class ReportService(
    IEventRepository events,
    IActivityRoleTypeRepository roleTypes,
    IActivityRepository activities,
    IUserRepository users,
    IResourceRepository resources,
    IAnnouncementRepository announcements,
    IPartnerRepository partners,
    IEventCategoryTypeRepository eventCategoryTypes,
    IDashboardRepository dashboard,
    IQueryExecutor executor,
    IClock clock,
    HybridCache cache
) : IReportService
{
    private static readonly SortMap<User> AttendeeSort = new SortMap<User>()
        .Add("firstName", u => u.FirstName)
        .Add("lastName", u => u.LastName)
        .Add("email", u => u.Email)
        .Add("phone", u => u.Phone)
        .Add("birthDate", u => u.BirthDate)
        .Add("type", u => u.UserType.Name)
        .Add("createdAt", u => u.CreatedAt)
        .Default("firstName")
        .Tie(u => u.Id);

    public async Task<Result<EventSummaryResponse>> GetEventSummaryAsync(
        Guid eventId,
        CancellationToken ct = default
    )
    {
        var ev = await executor.FirstOrDefaultAsync(
            events
                .Query()
                .Where(e => e.Id == eventId)
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    ActivitiesCount = e.Activities.Count,
                }),
            ct
        );
        if (ev is null)
            return Error.NotFound(ErrorCode.EventNotFound);

        var stats = await executor.FirstOrDefaultAsync(
            activities
                .QueryAssignments()
                .Where(a => a.Activity.EventId == eventId)
                .GroupBy(a => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Requested = g.Count(a =>
                        a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Requested
                    ),
                    Confirmed = g.Count(a =>
                        a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                    ),
                    Denied = g.Count(a =>
                        a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Denied
                    ),
                    DistinctUsers = g.Select(a => a.UserId).Distinct().Count(),
                }),
            ct
        );

        var roleTypeBreakdown = await executor.ToListAsync(
            roleTypes
                .Query()
                .OrderBy(role => role.Name)
                .Select(role => new EventRoleTypeSummaryResponse(
                    role.Id,
                    role.Name,
                    role.Assignments.Count(a =>
                        a.Activity.EventId == eventId
                        && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                    )
                )),
            ct
        );

        return new EventSummaryResponse(
            ev.Id,
            ev.Title,
            ev.ActivitiesCount,
            stats?.Total ?? 0,
            stats?.Requested ?? 0,
            stats?.Confirmed ?? 0,
            stats?.Denied ?? 0,
            stats?.DistinctUsers ?? 0,
            roleTypeBreakdown
        );
    }

    public async Task<PagedResult<EventAttendeeResponse>> ListEventAttendeesAsync(
        Guid eventId,
        EventAttendeeListQuery query,
        CancellationToken ct = default
    )
    {
        var activityId = query.ActivityId;
        var roleTypeId = query.RoleTypeId;
        var statusId = query.StatusId;

        var source = users
            .Query()
            .Where(u =>
                u.Assignments.Any(a =>
                    a.Activity.EventId == eventId
                    && (activityId == null || a.ActivityId == activityId)
                    && (roleTypeId == null || a.ActivityRoleTypeId == roleTypeId)
                    && (statusId == null || a.AssignmentStatusId == statusId)
                )
            );

        if (query.UserTypeId is { } userTypeId)
            source = source.Where(u => u.UserTypeId == userTypeId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            source = source.Where(
                TextSearch.Contains<User>(
                    u =>
                        u.FirstName
                        + " "
                        + u.LastName
                        + " "
                        + (u.Email ?? "")
                        + " "
                        + (u.Phone ?? "")
                        + (
                            u.Parent == null
                                ? ""
                                : " "
                                    + u.Parent.FirstName
                                    + " "
                                    + u.Parent.LastName
                                    + " "
                                    + (u.Parent.Email ?? "")
                                    + " "
                                    + (u.Parent.Phone ?? "")
                        ),
                    TextSearch.Normalize(query.Search)
                )
            );
        }

        var projected = AttendeeSort
            .Apply(source, query.Sort)
            .Select(u => new AttendeeRow(
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.Phone,
                u.BirthDate,
                u.UserType.Name,
                u.UserType.Color,
                u.Parent == null
                    ? null
                    : new EventAttendeeGuardianResponse(
                        u.Parent.FirstName,
                        u.Parent.LastName,
                        u.Parent.Email,
                        u.Parent.Phone
                    ),
                u.Assignments.Where(a =>
                        a.Activity.EventId == eventId
                        && (activityId == null || a.ActivityId == activityId)
                        && (roleTypeId == null || a.ActivityRoleTypeId == roleTypeId)
                        && (statusId == null || a.AssignmentStatusId == statusId)
                    )
                    .OrderBy(a => a.Activity.ActivityStartsAt)
                    .ThenBy(a => a.Activity.Title)
                    .Select(a => new AttendeeAssignmentRow(
                        a.ActivityId,
                        a.Activity.Title,
                        a.Activity.ActivityStartsAt,
                        a.Activity.ActivityEndsAt,
                        a.ActivityRoleTypeId,
                        a.ActivityRoleType.Name,
                        a.AssignmentStatusId,
                        a.AssignmentStatus.Name,
                        a.CreatedAt
                    ))
                    .ToList(),
                u.Assignments.Where(a =>
                        a.Activity.EventId == eventId
                        && a.AssignmentStatusId != SeedIds.AssignmentStatusTypes.Denied
                    )
                    .Select(a => new AssignmentWindow(
                        a.ActivityId,
                        a.Activity.ActivityStartsAt,
                        a.Activity.ActivityEndsAt
                    ))
                    .ToList()
            ));

        var page = await executor.ToPagedAsync(projected, query.Page, query.PageSize, ct);
        var items = page.Items.Select(ToAttendeeResponse).ToList();
        return new PagedResult<EventAttendeeResponse>(items, page.Total, page.Page, page.PageSize);
    }

    private static EventAttendeeResponse ToAttendeeResponse(AttendeeRow row)
    {
        var assignments = row
            .Assignments.Select(a => new EventAttendeeAssignmentResponse(
                a.ActivityId,
                a.ActivityTitle,
                a.ActivityStartsAt,
                a.ActivityEndsAt,
                a.RoleTypeId,
                a.RoleTypeName,
                a.StatusId,
                a.StatusName,
                a.SignedUpAt,
                a.StatusId != SeedIds.AssignmentStatusTypes.Denied
                    && row.Windows.Exists(w =>
                        w.ActivityId != a.ActivityId
                        && a.ActivityStartsAt < w.EndsAt
                        && w.StartsAt < a.ActivityEndsAt
                    )
            ))
            .ToList();

        return new EventAttendeeResponse(
            row.UserId,
            row.FirstName,
            row.LastName,
            row.Email,
            row.Phone,
            row.BirthDate,
            row.UserTypeName,
            row.UserTypeColor,
            row.Guardian,
            assignments
        );
    }

    private sealed record AttendeeRow(
        Guid UserId,
        string? FirstName,
        string? LastName,
        string? Email,
        string? Phone,
        DateOnly BirthDate,
        string UserTypeName,
        string UserTypeColor,
        EventAttendeeGuardianResponse? Guardian,
        List<AttendeeAssignmentRow> Assignments,
        List<AssignmentWindow> Windows
    );

    private sealed record AttendeeAssignmentRow(
        Guid ActivityId,
        string ActivityTitle,
        DateTimeOffset ActivityStartsAt,
        DateTimeOffset ActivityEndsAt,
        Guid RoleTypeId,
        string? RoleTypeName,
        Guid StatusId,
        string? StatusName,
        DateTimeOffset SignedUpAt
    );

    private sealed record AssignmentWindow(
        Guid ActivityId,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt
    );

    public async Task<Result<EventBadgesResponse>> GetEventBadgesAsync(
        Guid eventId,
        CancellationToken ct = default
    )
    {
        var ev = await GetEventHeaderAsync(eventId, ct);
        if (ev is null)
            return Error.NotFound(ErrorCode.EventNotFound);

        var rows = await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(a =>
                    a.Activity.EventId == eventId
                    && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                )
                .Select(a => new
                {
                    a.UserId,
                    a.User.FirstName,
                    a.User.LastName,
                    UserTypeName = a.User.UserType.Name,
                    UserTypeColor = a.User.UserType.Color,
                    a.User.CreatedAt,
                    Guardian = a.User.Parent == null
                        ? null
                        : new EventBadgeGuardianResponse(
                            a.User.Parent.FirstName,
                            a.User.Parent.LastName,
                            a.User.Parent.Phone
                        ),
                    a.ActivityId,
                    ActivityTitle = a.Activity.Title,
                    a.Activity.ActivityStartsAt,
                }),
            ct
        );

        var badges = rows.GroupBy(r => r.UserId)
            .Select(g =>
            {
                var user = g.First();
                return new EventBadgeResponse(
                    g.Key,
                    user.FirstName,
                    user.LastName,
                    user.UserTypeName,
                    user.UserTypeColor,
                    user.CreatedAt,
                    user.Guardian,
                    g.OrderBy(r => r.ActivityStartsAt)
                        .ThenBy(r => r.ActivityTitle, StringComparer.Ordinal)
                        .DistinctBy(r => r.ActivityId)
                        .Select(r => r.ActivityTitle)
                        .ToList()
                );
            })
            .OrderBy(b => TextSearch.Normalize(b.LastName), StringComparer.Ordinal)
            .ThenBy(b => TextSearch.Normalize(b.FirstName), StringComparer.Ordinal)
            .ThenBy(b => b.UserId)
            .ToList();

        return new EventBadgesResponse(ev.Id, ev.Title, badges);
    }

    public async Task<Result<EventRosterResponse>> GetEventRosterAsync(
        Guid eventId,
        CancellationToken ct = default
    )
    {
        var ev = await GetEventHeaderAsync(eventId, ct);
        if (ev is null)
            return Error.NotFound(ErrorCode.EventNotFound);

        var rows = await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(a =>
                    a.Activity.EventId == eventId
                    && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                )
                .Select(a => new
                {
                    a.ActivityId,
                    ActivityTitle = a.Activity.Title,
                    a.Activity.Location,
                    a.Activity.ActivityStartsAt,
                    a.Activity.ActivityEndsAt,
                    a.UserId,
                    a.User.FirstName,
                    a.User.LastName,
                    a.User.BirthDate,
                    a.User.Email,
                    a.User.Phone,
                    a.ActivityRoleTypeId,
                    RoleName = a.ActivityRoleType.Name,
                    Guardian = a.User.Parent == null
                        ? null
                        : new EventRosterGuardianResponse(
                            a.User.Parent.FirstName,
                            a.User.Parent.LastName,
                            a.User.Parent.Email,
                            a.User.Parent.Phone
                        ),
                }),
            ct
        );

        var rosterActivities = rows.GroupBy(r => r.ActivityId)
            .Select(g =>
            {
                var activity = g.First();
                return new EventRosterActivityResponse(
                    g.Key,
                    activity.ActivityTitle,
                    activity.Location,
                    activity.ActivityStartsAt,
                    activity.ActivityEndsAt,
                    g.OrderBy(r => RosterRolePriority(r.ActivityRoleTypeId))
                        .ThenBy(r => TextSearch.Normalize(r.FirstName), StringComparer.Ordinal)
                        .ThenBy(r => TextSearch.Normalize(r.LastName), StringComparer.Ordinal)
                        .ThenBy(r => r.UserId)
                        .DistinctBy(r => r.UserId)
                        .Select(r => new EventRosterParticipantResponse(
                            r.UserId,
                            r.FirstName,
                            r.LastName,
                            r.BirthDate,
                            r.Email,
                            r.Phone,
                            r.RoleName,
                            r.Guardian
                        ))
                        .ToList()
                );
            })
            .OrderBy(a => a.ActivityStartsAt)
            .ThenBy(a => a.Title, StringComparer.Ordinal)
            .ThenBy(a => a.ActivityId)
            .ToList();

        return new EventRosterResponse(ev.Id, ev.Title, rosterActivities);
    }

    private static int RosterRolePriority(Guid roleTypeId)
    {
        if (roleTypeId == SeedIds.ActivityRoleTypes.Leader)
            return 0;
        if (roleTypeId == SeedIds.ActivityRoleTypes.Volunteer)
            return 1;
        return roleTypeId == SeedIds.ActivityRoleTypes.Participant ? 2 : 3;
    }

    public async Task<DashboardSummaryResponse> GetDashboardSummaryAsync(
        CancellationToken ct = default
    )
    {
        return await cache.GetOrCreateAsync(
            "reports:dashboard",
            async token =>
            {
                var counts = await dashboard.GetCountsAsync(token);
                return new DashboardSummaryResponse(
                    counts.Events,
                    counts.Activities,
                    counts.Resources,
                    counts.Announcements,
                    counts.Partners,
                    counts.Users
                );
            },
            CachePolicies.Dashboard,
            [
                CacheTags.Events,
                CacheTags.Activities,
                CacheTags.Resources,
                CacheTags.Announcements,
                CacheTags.Partners,
                CacheTags.Users,
            ],
            ct
        );
    }

    public async Task<DashboardAnalyticsResponse> GetDashboardAnalyticsAsync(
        DashboardAnalyticsQuery query,
        CancellationToken ct = default
    )
    {
        var today = clock.Today;
        var end = query.To ?? today;
        var start = query.From ?? end.AddMonths(-12);
        if (start > end)
            (start, end) = (end, start);

        var totalDays = end.DayNumber - start.DayNumber + 1;
        var granularity =
            totalDays <= 45 ? "day"
            : totalDays <= 182 ? "week"
            : "month";

        return await cache.GetOrCreateAsync(
            $"reports:dashboard:analytics:{start:yyyy-MM-dd}:{end:yyyy-MM-dd}:{granularity}",
            async token => await BuildAnalyticsAsync(start, end, granularity, token),
            CachePolicies.Dashboard,
            [
                CacheTags.Events,
                CacheTags.Activities,
                CacheTags.Resources,
                CacheTags.Announcements,
                CacheTags.Partners,
                CacheTags.Users,
            ],
            ct
        );
    }

    private async Task<DashboardAnalyticsResponse> BuildAnalyticsAsync(
        DateOnly start,
        DateOnly end,
        string granularity,
        CancellationToken ct
    )
    {
        var tz = clock.TimeZone;
        var today = clock.Today;
        var now = clock.UtcNow;

        DateOnly LocalDate(DateTimeOffset value) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, tz).DateTime);

        var buckets = BuildBuckets(start, end, granularity);
        var bucketIndex = new Dictionary<DateOnly, int>(buckets.Count);
        for (var i = 0; i < buckets.Count; i++)
            bucketIndex[BucketStart(buckets[i], granularity)] = i;

        var rangeLowerUtc = LocalDayRange.LowerUtc(start, tz);
        var rangeUpperUtc = LocalDayRange.UpperExclusiveUtc(end, tz);
        var rangeLen = end.DayNumber - start.DayNumber + 1;
        var prevEnd = start.AddDays(-1);
        var prevStart = prevEnd.AddDays(-(rangeLen - 1));
        var prevLowerUtc = LocalDayRange.LowerUtc(prevStart, tz);
        var prevUpperUtc = LocalDayRange.UpperExclusiveUtc(prevEnd, tz);

        var userRows = await executor.ToListAsync(
            users
                .Query()
                .Select(u => new
                {
                    u.CreatedAt,
                    u.UserTypeId,
                    IsMinor = u.ParentId != null,
                }),
            ct
        );

        var assignmentRows = await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Select(a => new
                {
                    a.CreatedAt,
                    a.AssignmentStatusId,
                    a.Activity.EventId,
                }),
            ct
        );

        var eventRows = await executor.ToListAsync(
            events
                .Query()
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    e.CreatedAt,
                    e.EventStartsAt,
                }),
            ct
        );

        var categoryRows = await executor.ToListAsync(
            eventCategoryTypes
                .Query()
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Color,
                    Count = t.Events.Count,
                }),
            ct
        );

        var resourceRows = await executor.ToListAsync(
            resources.Query().Select(r => new { r.CreatedAt, r.ResourceTypeId }),
            ct
        );

        var announcementDates = await executor.ToListAsync(
            announcements.Query().Select(a => a.CreatedAt),
            ct
        );

        var partnerDates = await executor.ToListAsync(
            partners.Query().Select(p => p.CreatedAt),
            ct
        );

        var activityRows = await executor.ToListAsync(
            activities
                .Query()
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.CreatedAt,
                    a.ActivityStartsAt,
                    a.EventId,
                    EventTitle = a.Event.Title,
                    Desired = a.RoleCapacities.Sum(c => (int?)c.DesiredCount) ?? 0,
                    Confirmed = a.Assignments.Count(x =>
                        x.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                    ),
                }),
            ct
        );

        DashboardSeriesResponse Cumulative(string key, IEnumerable<DateTimeOffset> createdAts)
        {
            var before = 0;
            var perBucket = new int[buckets.Count];
            foreach (var ts in createdAts)
            {
                var day = LocalDate(ts);
                if (day < start)
                    before++;
                else if (
                    day <= end
                    && bucketIndex.TryGetValue(BucketStart(day, granularity), out var idx)
                )
                    perBucket[idx]++;
            }

            var values = new int[buckets.Count];
            var running = before;
            for (var i = 0; i < buckets.Count; i++)
            {
                running += perBucket[i];
                values[i] = running;
            }

            return new DashboardSeriesResponse(key, values);
        }

        DashboardSeriesResponse Flow(string key, IEnumerable<DateTimeOffset> createdAts)
        {
            var perBucket = new int[buckets.Count];
            foreach (var ts in createdAts)
            {
                var day = LocalDate(ts);
                if (
                    day >= start
                    && day <= end
                    && bucketIndex.TryGetValue(BucketStart(day, granularity), out var idx)
                )
                    perBucket[idx]++;
            }

            return new DashboardSeriesResponse(key, perBucket);
        }

        static int Between(
            IEnumerable<DateTimeOffset> dates,
            DateTimeOffset lo,
            DateTimeOffset hi
        ) => dates.Count(d => d >= lo && d < hi);

        var kpiSources = new (string Key, List<DateTimeOffset> Dates)[]
        {
            ("users", userRows.Select(u => u.CreatedAt).ToList()),
            (
                "members",
                userRows
                    .Where(u => u.UserTypeId == SeedIds.UserTypes.Member)
                    .Select(u => u.CreatedAt)
                    .ToList()
            ),
            ("inscriptions", assignmentRows.Select(a => a.CreatedAt).ToList()),
            ("events", eventRows.Select(e => e.CreatedAt).ToList()),
            ("activities", activityRows.Select(a => a.CreatedAt).ToList()),
            ("resources", resourceRows.Select(r => r.CreatedAt).ToList()),
            ("announcements", announcementDates.ToList()),
            ("partners", partnerDates.ToList()),
        };
        var kpis = kpiSources
            .Select(s => new DashboardKpiResponse(
                s.Key,
                s.Dates.Count,
                Between(s.Dates, rangeLowerUtc, rangeUpperUtc),
                Between(s.Dates, prevLowerUtc, prevUpperUtc)
            ))
            .ToList();

        var userGrowth = new DashboardTimeSeriesResponse(
            buckets,
            new[] { "member", "sponsor", "participant" }
                .Select(key =>
                    Cumulative(
                        key,
                        userRows
                            .Where(u => UserTypeKey(u.UserTypeId) == key)
                            .Select(u => u.CreatedAt)
                    )
                )
                .ToList()
        );

        var inscriptions = new DashboardTimeSeriesResponse(
            buckets,
            new[] { "requested", "confirmed", "denied" }
                .Select(key =>
                    Flow(
                        key,
                        assignmentRows
                            .Where(a => AssignmentStatusKey(a.AssignmentStatusId) == key)
                            .Select(a => a.CreatedAt)
                    )
                )
                .ToList()
        );

        var contentPublished = new DashboardTimeSeriesResponse(
            buckets,
            [
                Flow("announcements", announcementDates),
                Flow("resources", resourceRows.Select(r => r.CreatedAt)),
            ]
        );

        var usersByType = FixedSlices(
            ["member", "sponsor", "participant"],
            userRows
                .GroupBy(u => UserTypeKey(u.UserTypeId))
                .ToDictionary(g => g.Key, g => g.Count())
        );

        var audience = FixedSlices(
            ["adults", "minors"],
            new Dictionary<string, int>
            {
                ["adults"] = userRows.Count(u => !u.IsMinor),
                ["minors"] = userRows.Count(u => u.IsMinor),
            }
        );

        var resourcesByType = FixedSlices(
            ["internal", "external"],
            resourceRows
                .GroupBy(r => ResourceTypeKey(r.ResourceTypeId))
                .ToDictionary(g => g.Key, g => g.Count())
        );

        var eventsByCategory = categoryRows
            .Where(c => c.Count > 0)
            .OrderByDescending(c => c.Count)
            .ThenBy(c => TextSearch.Normalize(c.Name), StringComparer.Ordinal)
            .Select(c => new DashboardSliceResponse(c.Id.ToString(), c.Name, c.Color, c.Count))
            .ToList();

        var titleById = eventRows.ToDictionary(e => e.Id, e => e.Title);
        var topEvents = assignmentRows
            .Where(a => a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed)
            .GroupBy(a => a.EventId)
            .Select(g => new { EventId = g.Key, Confirmed = g.Count() })
            .OrderByDescending(x => x.Confirmed)
            .ThenBy(x => titleById.GetValueOrDefault(x.EventId, ""), StringComparer.Ordinal)
            .ThenBy(x => x.EventId)
            .Take(8)
            .Select(x => new DashboardTopEventResponse(
                x.EventId,
                titleById.GetValueOrDefault(x.EventId, ""),
                x.Confirmed
            ))
            .ToList();

        var calendarStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-6);
        var calendarBuckets = BuildBuckets(calendarStart, calendarStart.AddMonths(12), "month");
        var calendarIndex = new Dictionary<DateOnly, int>(calendarBuckets.Count);
        for (var i = 0; i < calendarBuckets.Count; i++)
            calendarIndex[calendarBuckets[i]] = i;

        var past = new int[calendarBuckets.Count];
        var upcoming = new int[calendarBuckets.Count];
        foreach (var ev in eventRows)
        {
            var monthStart = new DateOnly(ev.EventStartsAt.Year, ev.EventStartsAt.Month, 1);
            if (!calendarIndex.TryGetValue(monthStart, out var idx))
                continue;
            if (ev.EventStartsAt < today)
                past[idx]++;
            else
                upcoming[idx]++;
        }

        var eventsCalendar = new DashboardTimeSeriesResponse(
            calendarBuckets,
            [
                new DashboardSeriesResponse("past", past),
                new DashboardSeriesResponse("upcoming", upcoming),
            ]
        );

        var upcomingWithCapacity = activityRows
            .Where(a => a.ActivityStartsAt >= now && a.Desired > 0)
            .ToList();
        var occupancyEvents = upcomingWithCapacity
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
        var occupancy = new DashboardOccupancyResponse(
            upcomingWithCapacity.Sum(a => a.Confirmed),
            upcomingWithCapacity.Sum(a => a.Desired),
            occupancyEvents
        );

        return new DashboardAnalyticsResponse(
            start,
            end,
            granularity,
            kpis,
            userGrowth,
            inscriptions,
            contentPublished,
            usersByType,
            audience,
            resourcesByType,
            eventsByCategory,
            topEvents,
            eventsCalendar,
            occupancy
        );
    }

    private static List<DateOnly> BuildBuckets(DateOnly start, DateOnly end, string granularity)
    {
        var buckets = new List<DateOnly>();
        var cursor = BucketStart(start, granularity);
        while (cursor <= end)
        {
            buckets.Add(cursor);
            cursor = granularity switch
            {
                "day" => cursor.AddDays(1),
                "week" => cursor.AddDays(7),
                _ => cursor.AddMonths(1),
            };
        }

        return buckets;
    }

    private static DateOnly BucketStart(DateOnly date, string granularity) =>
        granularity switch
        {
            "day" => date,
            "week" => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
            _ => new DateOnly(date.Year, date.Month, 1),
        };

    private static IReadOnlyList<DashboardSliceResponse> FixedSlices(
        IReadOnlyList<string> keys,
        IReadOnlyDictionary<string, int> counts
    ) =>
        keys.Select(k => new DashboardSliceResponse(k, null, null, counts.GetValueOrDefault(k)))
            .ToList();

    private static string UserTypeKey(Guid id) =>
        id == SeedIds.UserTypes.Member ? "member"
        : id == SeedIds.UserTypes.Sponsor ? "sponsor"
        : id == SeedIds.UserTypes.Participant ? "participant"
        : "other";

    private static string AssignmentStatusKey(Guid id) =>
        id == SeedIds.AssignmentStatusTypes.Requested ? "requested"
        : id == SeedIds.AssignmentStatusTypes.Confirmed ? "confirmed"
        : id == SeedIds.AssignmentStatusTypes.Denied ? "denied"
        : "other";

    private static string ResourceTypeKey(Guid id) =>
        id == SeedIds.ResourceTypes.Internal ? "internal"
        : id == SeedIds.ResourceTypes.External ? "external"
        : "other";

    private Task<EventHeader?> GetEventHeaderAsync(Guid eventId, CancellationToken ct)
    {
        return executor.FirstOrDefaultAsync(
            events.Query().Where(e => e.Id == eventId).Select(e => new EventHeader(e.Id, e.Title)),
            ct
        );
    }

    private sealed record EventHeader(Guid Id, string Title);
}
