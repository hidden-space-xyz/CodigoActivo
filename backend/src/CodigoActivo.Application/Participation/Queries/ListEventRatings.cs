using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Participation.Queries;

public sealed record ListEventRatingsQuery(Guid EventId, EventRatingListQuery Filters)
    : IQuery<Result<PagedResult<EventRatingListItemResponse>>>;

public sealed class ListEventRatingsQueryHandler(
    IEventRepository events,
    IEventRatingRepository ratings,
    IQueryExecutor executor
) : IQueryHandler<ListEventRatingsQuery, Result<PagedResult<EventRatingListItemResponse>>>
{
    private static readonly SortMap<EventRatingListItemResponse> Sort =
        new SortMap<EventRatingListItemResponse>()
            .Add("score", r => r.Score)
            .Add("createdAt", r => r.CreatedAt)
            .Default("-createdAt")
            .Tie(r => r.Id);

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
