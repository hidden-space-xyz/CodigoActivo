using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Participation.Queries;

/// <summary>
/// Carries the criteria used to retrieve event history.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
public sealed record GetEventHistoryQuery(Guid UserId)
    : IQuery<IReadOnlyList<EventHistoryResponse>>;

/// <summary>
/// Executes the query to retrieve event history.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="submissions">Repository used to persist and retrieve rating submissions.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class GetEventHistoryQueryHandler(
    IActivityRepository activities,
    IEventRatingSubmissionRepository submissions,
    IQueryExecutor executor,
    IClock clock
) : IQueryHandler<GetEventHistoryQuery, IReadOnlyList<EventHistoryResponse>>
{
    /// <summary>
    /// Handles the request to retrieve event history.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching event history items.</returns>
    public async Task<IReadOnlyList<EventHistoryResponse>> HandleAsync(
        GetEventHistoryQuery query,
        CancellationToken ct = default
    )
    {
        var userId = query.UserId;

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

        var ratedEventIds = (
            await executor.ToListAsync(
                submissions.Query().Where(s => s.UserId == userId).Select(s => s.EventId),
                ct
            )
        ).ToHashSet();

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
                isPast && ratedEventIds.Contains(group.Key),
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

    private static EventHistoryResponse ToHistoryEntry(
        IReadOnlyList<HistoryRow> rows,
        bool isPast,
        bool hasRated,
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
            hasRated,
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

    private sealed record HistoryRow
    {
        /// <summary>
        /// Gets or sets the identifier of the associated event.
        /// </summary>
        public Guid EventId { get; init; }
        /// <summary>
        /// Gets or sets the event title value.
        /// </summary>
        public string EventTitle { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the event subtitle value.
        /// </summary>
        public string EventSubtitle { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the date and time when the event starts.
        /// </summary>
        public DateOnly EventStartsAt { get; init; }
        /// <summary>
        /// Gets or sets the date and time when the event ends.
        /// </summary>
        public DateOnly EventEndsAt { get; init; }
        /// <summary>
        /// Gets or sets the identifier of the associated thumbnail.
        /// </summary>
        public Guid ThumbnailId { get; init; }
        /// <summary>
        /// Gets or sets the identifier of the associated activity.
        /// </summary>
        public Guid ActivityId { get; init; }
        /// <summary>
        /// Gets or sets the activity title value.
        /// </summary>
        public string ActivityTitle { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the location value.
        /// </summary>
        public string Location { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the modality name value.
        /// </summary>
        public string ModalityName { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the identifier of the associated user.
        /// </summary>
        public Guid UserId { get; init; }
        /// <summary>
        /// Gets or sets the first name value.
        /// </summary>
        public string FirstName { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the last name value.
        /// </summary>
        public string LastName { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the identifier of the associated role type.
        /// </summary>
        public Guid RoleTypeId { get; init; }
        /// <summary>
        /// Gets or sets the role type name value.
        /// </summary>
        public string RoleTypeName { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the identifier of the associated status.
        /// </summary>
        public Guid StatusId { get; init; }
        /// <summary>
        /// Gets or sets the status name value.
        /// </summary>
        public string StatusName { get; init; } = string.Empty;
    }
}
