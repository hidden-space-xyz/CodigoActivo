using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Participation.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Participation.Queries;

/// <summary>
/// Carries the request for the participation history of the signed-in user and their dependents.
/// </summary>
public sealed record GetEventHistoryQuery : IQuery<IReadOnlyList<EventHistoryResponse>>;

/// <summary>
/// Executes the query for the events the signed-in user or their dependents signed up to. A past
/// event shows only the confirmed signups and is left out when it has none.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="currentUser">Person the query runs for.</param>
public sealed class GetEventHistoryQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    IClock clock,
    ICurrentUser currentUser
) : IQueryHandler<GetEventHistoryQuery, IReadOnlyList<EventHistoryResponse>>
{
    /// <summary>
    /// Handles the request for the participation history of the household.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the upcoming events, the soonest first, followed by the past
    /// ones, the most recently ended first.
    /// </returns>
    public async Task<IReadOnlyList<EventHistoryResponse>> HandleAsync(
        GetEventHistoryQuery query,
        CancellationToken ct = default
    )
    {
        var userId = currentUser.RequiredId().Value;

        var rows = await executor.ToListAsync(
            readStore
                .Assignments.Where(a => a.UserId == userId || a.User.ParentId == userId)
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

        var ratings = (
            await executor.ToListAsync(
                readStore
                    .EventRatings.Where(r => r.UserId == userId)
                    .Select(r => new
                    {
                        r.EventId,
                        Rating = new EventHistoryRatingResponse(
                            r.Score,
                            r.MostLiked,
                            r.LeastLiked,
                            r.Suggestions
                        ),
                    }),
                ct
            )
        ).ToDictionary(r => r.EventId, r => r.Rating);

        var today = clock.Today;
        var upcoming = new List<EventHistoryResponse>();
        var past = new List<EventHistoryResponse>();

        foreach (var group in rows.GroupBy(row => row.EventId))
        {
            var isPast = group.First().EventEndsAt < today;
            var visible = isPast
                ? group.Where(row => row.StatusId == ActivityProjections.ConfirmedStatusId).ToList()
                : [.. group];

            if (visible.Count is 0)
            {
                continue;
            }

            (isPast ? past : upcoming).Add(
                ToHistoryEntry(visible, isPast, ratings.GetValueOrDefault(group.Key), userId)
            );
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
        EventHistoryRatingResponse? rating,
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
            rating,
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
        /// Gets or sets the first day of the event.
        /// </summary>
        public DateOnly EventStartsAt { get; init; }

        /// <summary>
        /// Gets or sets the last day of the event.
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
