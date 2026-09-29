using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.News.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.News.Queries;

/// <summary>
/// Carries the criteria used to retrieve news item by identifier.
/// </summary>
/// <param name="NewsItemId">Identifier of the news item.</param>
public sealed record GetNewsItemByIdQuery(Guid NewsItemId) : IQuery<Result<NewsItemResponse>>;

/// <summary>
/// Executes the query to retrieve news item by identifier.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetNewsItemByIdQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetNewsItemByIdQuery, Result<NewsItemResponse>>
{
    /// <summary>
    /// Handles the request to retrieve news item by identifier.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a news item on success, or an application error on failure.</returns>
    public async Task<Result<NewsItemResponse>> HandleAsync(
        GetNewsItemByIdQuery query,
        CancellationToken ct = default
    )
    {
        var response = await executor.FirstOrDefaultAsync(
            readStore.News.Where(a => a.Id == query.NewsItemId).Select(NewsProjections.NewsItem),
            ct
        );
        return response is null ? Error.NotFound(ErrorCode.NewsItemNotFound) : response;
    }
}
