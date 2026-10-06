using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Reports.Queries;

/// <summary>
/// Carries the criteria used to retrieve event badges.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
public sealed record GetEventBadgesQuery(EventId EventId) : IQuery<Result<EventBadgesResponse>>;

/// <summary>
/// Executes the query to retrieve event badges.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetEventBadgesQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetEventBadgesQuery, Result<EventBadgesResponse>>
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
        var eventId = query.EventId.Value;
        var ev = await GetEventHeaderAsync(eventId, ct);
        if (ev is null)
        {
            return Error.NotFound(ApplicationErrorCode.EventNotFound);
        }

        var rows = await executor.ToListAsync(
            readStore
                .Assignments.Where(a =>
                    a.Activity.EventId == eventId
                    && a.AssignmentStatusId
                        == CatalogIds.AssignmentStatuses.IdOf(AssignmentStatus.Confirmed)
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
            readStore
                .Events.Where(e => e.Id == eventId)
                .Select(e => new EventHeader(e.Id, e.Title)),
            ct
        );
    }

    private sealed record EventHeader(Guid Id, string Title);
}
