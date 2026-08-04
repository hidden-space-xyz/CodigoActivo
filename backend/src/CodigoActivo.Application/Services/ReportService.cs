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

    private static readonly string[] UserGrowthKeys = ["member", "sponsor", "participant"];

    private static readonly string[] InscriptionKeys = ["requested", "confirmed", "denied"];

    private static readonly string[] GenderKeys = ["Male", "Female", "Other"];

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
                    RatingsCount = e.Ratings.Count,
                    RatingsAverage = e.Ratings.Average(rating => (double?)rating.Score),
                }),
            ct
        );
        if (ev is null)
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

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
            ev.RatingsCount,
            ev.RatingsAverage,
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

        var source = UserFilters.ApplyEventAttendees(users.Query(), eventId, query);

        var projected = AttendeeSort
            .Apply(source, query.Sort)
            .Select(u => new AttendeeRow(
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.Phone,
                u.BirthDate,
                u.Gender,
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
                    .Select(a => new EventAttendeeAssignmentResponse(
                        a.ActivityId,
                        a.Activity.Title,
                        a.Activity.ActivityStartsAt,
                        a.Activity.ActivityEndsAt,
                        a.ActivityRoleTypeId,
                        a.ActivityRoleType.Name,
                        a.AssignmentStatusId,
                        a.AssignmentStatus.Name,
                        a.CreatedAt,
                        false
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
        var assignments = row.Assignments.ConvertAll(a =>
            a with
            {
                HasTimeConflict = a.StatusId != SeedIds.AssignmentStatusTypes.Denied
                    && row.Windows.Exists(w =>
                        w.ActivityId != a.ActivityId
                        && a.ActivityStartsAt < w.EndsAt
                        && w.StartsAt < a.ActivityEndsAt
                    ),
            }
        );

        return new EventAttendeeResponse(
            row.UserId,
            row.FirstName,
            row.LastName,
            row.Email,
            row.Phone,
            row.BirthDate,
            row.Gender,
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
        Gender Gender,
        string UserTypeName,
        string UserTypeColor,
        EventAttendeeGuardianResponse? Guardian,
        List<EventAttendeeAssignmentResponse> Assignments,
        List<AssignmentWindow> Windows
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
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

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
                    [
                        .. g.OrderBy(r => r.ActivityStartsAt)
                            .ThenBy(r => r.ActivityTitle, StringComparer.Ordinal)
                            .DistinctBy(r => r.ActivityId)
                            .Select(r => r.ActivityTitle),
                    ]
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
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

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
                    [
                        .. g.OrderBy(r => RosterRolePriority(r.ActivityRoleTypeId))
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
                            )),
                    ]
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
        return roleTypeId switch
        {
            _ when roleTypeId == SeedIds.ActivityRoleTypes.Leader => 0,
            _ when roleTypeId == SeedIds.ActivityRoleTypes.Volunteer => 1,
            _ when roleTypeId == SeedIds.ActivityRoleTypes.Participant => 2,
            _ => 3,
        };
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
            CacheTags.DashboardSources,
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

        return await cache.GetOrCreateAsync(
            $"reports:dashboard:analytics:{start:yyyy-MM-dd}:{end:yyyy-MM-dd}:{granularity}",
            async token => await BuildAnalyticsAsync(start, end, granularity, token),
            CachePolicies.Dashboard,
            CacheTags.DashboardSources,
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

        DateOnly LocalDate(DateTimeOffset value)
        {
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, tz).DateTime);
        }

        var buckets = BuildBuckets(start, end, granularity);
        var bucketIndex = new Dictionary<DateOnly, int>(buckets.Count);
        for (var i = 0; i < buckets.Count; i++)
        {
            bucketIndex[BucketStart(buckets[i], granularity)] = i;
        }

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
                    u.Gender,
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
                    t.Events.Count,
                }),
            ct
        );

        var resourceDates = await executor.ToListAsync(
            resources.Query().Select(r => r.CreatedAt),
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

        (int[] PerBucket, int Before) Distribute(IEnumerable<DateTimeOffset> createdAts)
        {
            var before = 0;
            var perBucket = new int[buckets.Count];
            foreach (var ts in createdAts)
            {
                var day = LocalDate(ts);
                if (day < start)
                {
                    before++;
                }
                else if (
                    day <= end
                    && bucketIndex.TryGetValue(BucketStart(day, granularity), out var idx)
                )
                {
                    perBucket[idx]++;
                }
            }

            return (perBucket, before);
        }

        DashboardSeriesResponse Cumulative(string key, IEnumerable<DateTimeOffset> createdAts)
        {
            var (perBucket, before) = Distribute(createdAts);

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
            return new DashboardSeriesResponse(key, Distribute(createdAts).PerBucket);
        }

        static int Between(
            IEnumerable<DateTimeOffset> dates,
            DateTimeOffset lo,
            DateTimeOffset hi
        )
        {
            return dates.Count(d => d >= lo && d < hi);
        }

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
            ("resources", resourceDates.ToList()),
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

        var usersByGrowthKey = userRows.ToLookup(
            u => UserTypeKey(u.UserTypeId),
            u => u.CreatedAt,
            StringComparer.Ordinal
        );
        var userGrowth = new DashboardTimeSeriesResponse(
            buckets,
            [.. UserGrowthKeys.Select(key => Cumulative(key, usersByGrowthKey[key]))]
        );

        var inscriptionsByStatus = assignmentRows.ToLookup(
            a => AssignmentStatusKey(a.AssignmentStatusId),
            a => a.CreatedAt,
            StringComparer.Ordinal
        );
        var inscriptions = new DashboardTimeSeriesResponse(
            buckets,
            [.. InscriptionKeys.Select(key => Flow(key, inscriptionsByStatus[key]))]
        );

        var contentPublished = new DashboardTimeSeriesResponse(
            buckets,
            [
                Flow("announcements", announcementDates),
                Flow("resources", resourceDates),
            ]
        );

        var usersByType = FixedSlices(
            UserGrowthKeys,
            usersByGrowthKey.ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal)
        );

        var audience = FixedSlices(
            ["adults", "minors"],
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["adults"] = userRows.Count(u => !u.IsMinor),
                ["minors"] = userRows.Count(u => u.IsMinor),
            }
        );

        var participantsByGender = FixedSlices(
            GenderKeys,
            userRows
                .Where(u => u.UserTypeId == SeedIds.UserTypes.Participant)
                .GroupBy(u => u.Gender.ToString(), StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal)
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
        {
            calendarIndex[calendarBuckets[i]] = i;
        }

        var past = new int[calendarBuckets.Count];
        var upcoming = new int[calendarBuckets.Count];
        foreach (var eventStartsAt in eventRows.Select(ev => ev.EventStartsAt))
        {
            var monthStart = new DateOnly(eventStartsAt.Year, eventStartsAt.Month, 1);
            if (!calendarIndex.TryGetValue(monthStart, out var idx))
            {
                continue;
            }

            if (eventStartsAt < today)
            {
                past[idx]++;
            }
            else
            {
                upcoming[idx]++;
            }
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
            participantsByGender,
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

    private static DateOnly BucketStart(DateOnly date, string granularity)
    {
        return granularity switch
        {
            "day" => date,
            "week" => date.AddDays(-((date.DayOfWeek - DayOfWeek.Monday + 7) % 7)),
            _ => new DateOnly(date.Year, date.Month, 1),
        };
    }

    private static List<DashboardSliceResponse> FixedSlices(
        IReadOnlyList<string> keys,
        IReadOnlyDictionary<string, int> counts
    )
    {
        return [.. keys.Select(k => new DashboardSliceResponse(k, null, null, counts.GetValueOrDefault(k)))];
    }

    private static string UserTypeKey(Guid id)
    {
        return id switch
        {
            _ when id == SeedIds.UserTypes.Member => "member",
            _ when id == SeedIds.UserTypes.Sponsor => "sponsor",
            _ when id == SeedIds.UserTypes.Participant => "participant",
            _ => "other",
        };
    }

    private static string AssignmentStatusKey(Guid id)
    {
        return id switch
        {
            _ when id == SeedIds.AssignmentStatusTypes.Requested => "requested",
            _ when id == SeedIds.AssignmentStatusTypes.Confirmed => "confirmed",
            _ when id == SeedIds.AssignmentStatusTypes.Denied => "denied",
            _ => "other",
        };
    }

    private Task<EventHeader?> GetEventHeaderAsync(Guid eventId, CancellationToken ct)
    {
        return executor.FirstOrDefaultAsync(
            events.Query().Where(e => e.Id == eventId).Select(e => new EventHeader(e.Id, e.Title)),
            ct
        );
    }

    private sealed record EventHeader(Guid Id, string Title);
}
