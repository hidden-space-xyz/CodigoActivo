using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Reports.Queries;

/// <summary>
/// Carries the criteria used to retrieve event badges.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
public sealed record GetEventBadgesQuery(Guid EventId) : IQuery<Result<EventBadgesResponse>>;

/// <summary>
/// Executes the query to retrieve event badges.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetEventBadgesQueryHandler(
    IEventRepository events,
    IActivityRepository activities,
    IQueryExecutor executor
) : IQueryHandler<GetEventBadgesQuery, Result<EventBadgesResponse>>
{
    /// <summary>
    /// Handles the request to retrieve event badges.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an event badges on success, or an application error on failure.</returns>
    public async Task<Result<EventBadgesResponse>> HandleAsync(
        GetEventBadgesQuery query,
        CancellationToken ct = default
    )
    {
        var eventId = query.EventId;
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
                    ActivityLocation = a.Activity.Location,
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
                            .Select(r => new EventBadgeActivityResponse(
                                r.ActivityTitle,
                                r.ActivityLocation
                            )),
                    ]
                );
            })
            .OrderBy(b => TextSearch.Normalize(b.LastName), StringComparer.Ordinal)
            .ThenBy(b => TextSearch.Normalize(b.FirstName), StringComparer.Ordinal)
            .ThenBy(b => b.UserId)
            .ToList();

        return new EventBadgesResponse(ev.Id, ev.Title, badges);
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
