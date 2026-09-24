using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.News.Commands;

/// <summary>
/// Carries the input required to delete the news item.
/// </summary>
/// <param name="NewsItemId">Identifier of the news item.</param>
public sealed record DeleteNewsItemCommand(Guid NewsItemId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the news item.
/// </summary>
/// <param name="news">Repository used to persist and retrieve news items.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class DeleteNewsItemCommandHandler(
    INewsItemRepository news,
    IOrphanFileCleaner orphanCleaner,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeleteNewsItemCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the news item.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        DeleteNewsItemCommand command,
        CancellationToken ct = default
    )
    {
        var newsItem = await news.FindAsync(a => a.Id == command.NewsItemId, ct);
        if (newsItem is null)
        {
            return Error.NotFound(ErrorCode.NewsItemNotFound);
        }

        news.Remove(newsItem);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.News);

        var orphanCandidates = RichTextFileReferences
            .Extract(newsItem.Description)
            .Append(newsItem.ThumbnailId)
            .Distinct()
            .ToList();
        await orphanCleaner.DeleteOrphanedAsync(orphanCandidates, ct);

        return Result.Success();
    }
}
