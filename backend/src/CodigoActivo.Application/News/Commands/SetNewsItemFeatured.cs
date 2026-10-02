using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.News;

namespace CodigoActivo.Application.News.Commands;

/// <summary>
/// Carries the input required to set news item featured.
/// </summary>
/// <param name="NewsItemId">Identifier of the news item.</param>
public sealed record SetNewsItemFeaturedCommand(Guid NewsItemId) : ICommand<Result>;

/// <summary>
/// Executes the command to set news item featured.
/// </summary>
/// <param name="news">Repository used to persist and retrieve news items.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class SetNewsItemFeaturedCommandHandler(
    INewsItemRepository news,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<SetNewsItemFeaturedCommand, Result>
{
    /// <summary>
    /// Handles the request to set news item featured.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        SetNewsItemFeaturedCommand command,
        CancellationToken ct = default
    )
    {
        var chosen = await news.GetByIdAsync(command.NewsItemId, ct);
        if (chosen is null)
        {
            return Error.NotFound(ErrorCode.NewsItemNotFound);
        }

        await uow.ExecuteInTransactionAsync(
            async attempt =>
            {
                FeaturedSelection.UnfeatureAllBut(chosen, await news.ListFeaturedAsync(attempt));
                await uow.SaveChangesAsync(attempt);
                chosen.Feature();
                await uow.SaveChangesAsync(attempt);
                return true;
            },
            ct
        );

        await cacheInvalidator.InvalidateAsync(CacheTags.News);
        return Result.Success();
    }
}
