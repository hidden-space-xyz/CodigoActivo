using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Reports.Queries;

/// <summary>
/// Carries the criteria used to retrieve event summary.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
public sealed record GetEventSummaryQuery(EventId EventId) : IQuery<Result<EventSummaryResponse>>;

/// <summary>
/// Executes the query to retrieve event summary.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetEventSummaryQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetEventSummaryQuery, Result<EventSummaryResponse>>
{
    /// <summary>
    /// Handles the request to retrieve event summary.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an event summary on success, or an application error on failure.</returns>
    public async Task<Result<EventSummaryResponse>> HandleAsync(
        GetEventSummaryQuery query,
        CancellationToken ct = default
    )
    {
        var eventId = query.EventId.Value;
        var ev = await executor.FirstOrDefaultAsync(
            readStore
                .Events.Where(e => e.Id == eventId)
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    ActivitiesCount = e.Activities.Count,
                    RatingsCount = e.Ratings.Count,
                    ScoredRatingsCount = e.Ratings.Count(rating => rating.Score != null),
                    RatingsAverage = e
                        .Ratings.Where(rating => rating.Score != null)
                        .Average(rating => (double?)rating.Score),
                }),
            ct
        );
        if (ev is null)
        {
            return Error.NotFound(ApplicationErrorCode.EventNotFound);
        }

        var stats = await executor.FirstOrDefaultAsync(
            readStore
                .Assignments.Where(a => a.Activity.EventId == eventId)
                .GroupBy(a => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Requested = g.Count(a =>
                        a.AssignmentStatusId
                        == CatalogIds.AssignmentStatuses.IdOf(AssignmentStatus.Requested)
                    ),
                    Confirmed = g.Count(a =>
                        a.AssignmentStatusId
                        == CatalogIds.AssignmentStatuses.IdOf(AssignmentStatus.Confirmed)
                    ),
                    Denied = g.Count(a =>
                        a.AssignmentStatusId
                        == CatalogIds.AssignmentStatuses.IdOf(AssignmentStatus.Denied)
                    ),
                    DistinctUsers = g.Select(a => a.UserId).Distinct().Count(),
                }),
            ct
        );

        var roleTypeBreakdown = await executor.ToListAsync(
            readStore
                .ActivityRoleTypes.OrderBy(role => role.Name)
                .Select(role => new EventRoleTypeSummaryResponse(
                    role.Id,
                    role.Name,
                    role.Assignments.Count(a =>
                        a.Activity.EventId == eventId
                        && a.AssignmentStatusId
                            == CatalogIds.AssignmentStatuses.IdOf(AssignmentStatus.Confirmed)
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
            ev.ScoredRatingsCount,
            ev.RatingsAverage,
            roleTypeBreakdown
        );
    }
}
