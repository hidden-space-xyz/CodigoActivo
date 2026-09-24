using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.News.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.News.Commands;

/// <summary>
/// Carries the input required to set news item featured.
/// </summary>
/// <param name="NewsItemId">Identifier of the news item.</param>
public sealed record SetNewsItemFeaturedCommand(Guid NewsItemId)
    : ICommand<Result<NewsItemResponse>>;

/// <summary>
/// Executes the command to set news item featured.
/// </summary>
/// <param name="news">Repository used to persist and retrieve news items.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
/// <param name="getById">Handler used to retrieve news item by identifier.</param>
public sealed class SetNewsItemFeaturedCommandHandler(
    INewsItemRepository news,
    ICacheInvalidator cacheInvalidator,
    GetNewsItemByIdQueryHandler getById
) : ICommandHandler<SetNewsItemFeaturedCommand, Result<NewsItemResponse>>
{
    /// <summary>
    /// Handles the request to set news item featured.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a news item on success, or an application error on failure.</returns>
    public async Task<Result<NewsItemResponse>> HandleAsync(
        SetNewsItemFeaturedCommand command,
        CancellationToken ct = default
    )
    {
        if (!await news.SetFeaturedAsync(command.NewsItemId, ct))
        {
            return Error.NotFound(ErrorCode.NewsItemNotFound);
        }

        await cacheInvalidator.InvalidateAsync(CacheTags.News);
        return await getById.HandleAsync(new GetNewsItemByIdQuery(command.NewsItemId), ct);
    }
}
