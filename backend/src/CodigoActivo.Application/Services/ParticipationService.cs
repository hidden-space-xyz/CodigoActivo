using System.Globalization;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Services;

public class ParticipationService(
    IEventRepository events,
    IEventRatingRepository ratings,
    IActivityRepository activities,
    IQueryExecutor executor,
    IUnitOfWork unitOfWork,
    IClock clock
) : IParticipationService
{
    private const string CertificateCodePrefix = "CA";
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static readonly SortMap<EventRatingListItemResponse> RatingSort =
        new SortMap<EventRatingListItemResponse>()
            .Add("score", r => r.Score)
            .Add("createdAt", r => r.CreatedAt)
            .Default("-createdAt")
            .Tie(r => r.Id);

    public async Task<IReadOnlyList<EventHistoryResponse>> GetHistoryAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var rows = await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(a => a.UserId == userId || a.User.ParentId == userId)
                .OrderBy(a => a.Activity.Event.EventStartsAt)
                .ThenBy(a => a.Activity.ActivityStartsAt)
                .ThenBy(a => a.Activity.Title)
                .ThenBy(a => a.User.FirstName)
                .ThenBy(a => a.User.LastName)
                .Select(a => new HistoryRow
                {
                    EventId = a.Activity.EventId,
                    EventTitle = a.Activity.Event.Title,
                    EventSubtitle = a.Activity.Event.Subtitle,
                    EventStartsAt = a.Activity.Event.EventStartsAt,
                    EventEndsAt = a.Activity.Event.EventEndsAt,
                    ThumbnailId = a.Activity.Event.ThumbnailId,
                    ActivityId = a.ActivityId,
                    ActivityTitle = a.Activity.Title,
                    Location = a.Activity.Location,
                    ModalityName = a.Activity.ActivityModalityType.Name,
                    UserId = a.UserId,
                    FirstName = a.User.FirstName,
                    LastName = a.User.LastName,
                    RoleTypeId = a.ActivityRoleTypeId,
                    RoleTypeName = a.ActivityRoleType.Name,
                    StatusId = a.AssignmentStatusId,
                    StatusName = a.AssignmentStatus.Name,
                }),
            ct
        );

        if (rows.Count is 0)
        {
            return [];
        }

        var ownRatings = (
            await executor.ToListAsync(
                ratings.Query().Where(r => r.UserId == userId).Select(Projections.EventRating),
                ct
            )
        ).ToDictionary(rating => rating.EventId);

        var today = clock.Today;
        var upcoming = new List<EventHistoryResponse>();
        var past = new List<EventHistoryResponse>();

        foreach (var group in rows.GroupBy(row => row.EventId))
        {
            var isPast = group.First().EventEndsAt < today;
            var visible = isPast
                ? group
                    .Where(row => row.StatusId == SeedIds.AssignmentStatusTypes.Confirmed)
                    .ToList()
                : [.. group];

            if (visible.Count is 0)
            {
                continue;
            }

            var entry = ToHistoryEntry(
                visible,
                isPast,
                isPast ? ownRatings.GetValueOrDefault(group.Key) : null,
                userId
            );
            (isPast ? past : upcoming).Add(entry);
        }

        return
        [
            .. upcoming.OrderBy(e => e.EventStartsAt).ThenBy(e => e.EventId),
            .. past.OrderByDescending(e => e.EventEndsAt).ThenBy(e => e.EventId),
        ];
    }

    public async Task<IReadOnlyList<EventCertificateResponse>> GetCertificatesAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var today = clock.Today;

        var rows = await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(a =>
                    (a.UserId == userId || a.User.ParentId == userId)
                    && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                    && a.Activity.Event.EventEndsAt < today
                )
                .Select(a => new CertificateRow
                {
                    EventId = a.Activity.EventId,
                    EventTitle = a.Activity.Event.Title,
                    EventSubtitle = a.Activity.Event.Subtitle,
                    EventStartsAt = a.Activity.Event.EventStartsAt,
                    EventEndsAt = a.Activity.Event.EventEndsAt,
                    UserId = a.UserId,
                    FirstName = a.User.FirstName,
                    LastName = a.User.LastName,
                }),
            ct
        );

        return
        [
            .. rows.DistinctBy(row => (row.EventId, row.UserId))
                .OrderByDescending(row => row.EventEndsAt)
                .ThenBy(row => row.EventId)
                .ThenByDescending(row => row.UserId == userId)
                .ThenBy(row => TextSearch.Normalize(row.FirstName), StringComparer.Ordinal)
                .ThenBy(row => TextSearch.Normalize(row.LastName), StringComparer.Ordinal)
                .ThenBy(row => row.UserId)
                .Select(row => new EventCertificateResponse(
                    BuildCertificateCode(row.EventId, row.UserId, row.EventEndsAt.Year),
                    row.EventId,
                    row.UserId,
                    row.FirstName,
                    row.LastName,
                    row.UserId == userId,
                    row.EventTitle,
                    row.EventSubtitle,
                    row.EventStartsAt,
                    row.EventEndsAt
                )),
        ];
    }

    public async Task<Result<EventRatingResponse>> SaveRatingAsync(
        Guid eventId,
        Guid userId,
        SaveEventRatingRequest request,
        CancellationToken ct = default
    )
    {
        var ends = await executor.FirstOrDefaultAsync(
            events.Query().Where(e => e.Id == eventId).Select(e => (DateOnly?)e.EventEndsAt),
            ct
        );
        if (ends is not { } eventEndsAt)
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        if (eventEndsAt >= clock.Today)
        {
            return Error.Conflict(ErrorCode.EventRatingNotFinished);
        }

        var attended = await executor.FirstOrDefaultAsync(
            activities
                .QueryAssignments()
                .Where(a =>
                    a.Activity.EventId == eventId
                    && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                    && (a.UserId == userId || a.User.ParentId == userId)
                )
                .Select(a => (Guid?)a.ActivityId),
            ct
        );
        if (attended is null)
        {
            return Error.Conflict(ErrorCode.EventRatingAttendanceRequired);
        }

        var now = clock.UtcNow;
        var rating = await ratings.FindAsync(r => r.EventId == eventId && r.UserId == userId, ct);

        if (rating is null)
        {
            rating = new EventRating
            {
                EventId = eventId,
                UserId = userId,
                CreatedAt = now,
            };
            await ratings.AddAsync(rating, ct);
        }
        else
        {
            rating.UpdatedAt = now;
        }

        rating.Apply(
            request.Score!.Value,
            request.MostLiked,
            request.LeastLiked,
            request.Suggestions
        );

        await unitOfWork.SaveChangesAsync(ct);
        return rating.ToResponse();
    }

    public async Task<Result<PagedResult<EventRatingListItemResponse>>> ListEventRatingsAsync(
        Guid eventId,
        EventRatingListQuery query,
        CancellationToken ct = default
    )
    {
        if (!await events.ExistsAsync(e => e.Id == eventId, ct))
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        var source = ratings
            .Query()
            .Where(r => r.EventId == eventId)
            .Select(Projections.EventRatingListItem);

        return await executor.ToPagedAsync(
            RatingSort.Apply(source, query.Sort),
            query.Page,
            query.PageSize,
            ct
        );
    }

    private static string BuildCertificateCode(Guid eventId, Guid userId, int year)
    {
        Span<byte> eventBytes = stackalloc byte[16];
        Span<byte> userBytes = stackalloc byte[16];
        eventId.TryWriteBytes(eventBytes);
        userId.TryWriteBytes(userBytes);

        var hash = FnvOffsetBasis;
        unchecked
        {
            for (var i = 0; i < eventBytes.Length; i++)
            {
                hash = (hash ^ eventBytes[i]) * FnvPrime;
                hash = (hash ^ userBytes[i]) * FnvPrime;
            }
        }

        var digest = hash.ToString("X16", CultureInfo.InvariantCulture);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{CertificateCodePrefix}-{year:D4}-{digest[8..]}"
        );
    }

    private static EventHistoryResponse ToHistoryEntry(
        IReadOnlyList<HistoryRow> rows,
        bool isPast,
        EventRatingResponse? myRating,
        Guid userId
    )
    {
        var first = rows[0];
        return new EventHistoryResponse(
            first.EventId,
            first.EventTitle,
            first.EventSubtitle,
            first.EventStartsAt,
            first.EventEndsAt,
            first.ThumbnailId,
            isPast,
            isPast,
            myRating,
            [
                .. rows.Select(row => new EventHistoryActivityResponse(
                    row.ActivityId,
                    row.ActivityTitle,
                    row.Location,
                    row.ModalityName,
                    row.UserId,
                    row.FirstName,
                    row.LastName,
                    row.UserId == userId,
                    row.RoleTypeId,
                    row.RoleTypeName,
                    row.StatusId,
                    row.StatusName
                )),
            ]
        );
    }

    private sealed class CertificateRow
    {
        public Guid EventId { get; init; }
        public string EventTitle { get; init; } = string.Empty;
        public string EventSubtitle { get; init; } = string.Empty;
        public DateOnly EventStartsAt { get; init; }
        public DateOnly EventEndsAt { get; init; }
        public Guid UserId { get; init; }
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
    }

    private sealed class HistoryRow
    {
        public Guid EventId { get; init; }
        public string EventTitle { get; init; } = string.Empty;
        public string EventSubtitle { get; init; } = string.Empty;
        public DateOnly EventStartsAt { get; init; }
        public DateOnly EventEndsAt { get; init; }
        public Guid ThumbnailId { get; init; }
        public Guid ActivityId { get; init; }
        public string ActivityTitle { get; init; } = string.Empty;
        public string Location { get; init; } = string.Empty;
        public string ModalityName { get; init; } = string.Empty;
        public Guid UserId { get; init; }
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public Guid RoleTypeId { get; init; }
        public string RoleTypeName { get; init; } = string.Empty;
        public Guid StatusId { get; init; }
        public string StatusName { get; init; } = string.Empty;
    }
}
