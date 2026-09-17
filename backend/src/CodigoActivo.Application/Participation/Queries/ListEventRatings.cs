using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Participation.Queries;

/// <summary>
/// Carries the criteria used to list event ratings.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListEventRatingsQuery(Guid EventId, EventRatingListQuery Filters)
    : IQuery<Result<PagedResult<EventRatingListItemResponse>>>;

/// <summary>
/// Executes the query to list event ratings.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="ratings">Repository used to persist and retrieve ratings.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class ListEventRatingsQueryHandler(
    IEventRepository events,
    IEventRatingRepository ratings,
    IQueryExecutor executor
) : IQueryHandler<ListEventRatingsQuery, Result<PagedResult<EventRatingListItemResponse>>>
{
    private static readonly SortMap<EventRatingListItemResponse> Sort =
        new SortMap<EventRatingListItemResponse>()
            .Add("score", r => r.Score)
            .Default("-score")
            .Tie(r => r.Id);

    /// <summary>
    /// Handles the request to list event ratings.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a paged event rating list item on success, or an application error on failure.</returns>
    public async Task<Result<PagedResult<EventRatingListItemResponse>>> HandleAsync(
        ListEventRatingsQuery query,
        CancellationToken ct = default
    )
    {
        if (!await events.ExistsAsync(e => e.Id == query.EventId, ct))
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        var source = ratings
            .Query()
            .Where(r => r.EventId == query.EventId)
            .Select(Projections.EventRatingListItem);

        return await executor.ToPagedAsync(
            Sort.Apply(source, query.Filters.Sort),
            query.Filters.Page,
            query.Filters.PageSize,
            ct
        );
    }
}
