using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.News.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;

namespace CodigoActivo.Application.News.Commands;

/// <summary>
/// Carries the input required to update the news item.
/// </summary>
/// <param name="NewsItemId">Identifier of the news item.</param>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record UpdateNewsItemCommand(
    Guid NewsItemId,
    UpdateNewsItemRequest Request,
    Guid UserId
) : ICommand<Result>;

/// <summary>
/// Executes the command to update the news item.
/// </summary>
/// <param name="news">Repository used to persist and retrieve news items.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class UpdateNewsItemCommandHandler(
    INewsItemRepository news,
    IStoredFileRepository files,
    IOrphanFileCleaner orphanCleaner,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UpdateNewsItemCommand, Result>
{
    /// <summary>
    /// Handles the request to update the news item.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        UpdateNewsItemCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var newsItem = await news.GetByIdAsync(command.NewsItemId, ct);
        if (newsItem is null)
        {
            return Error.NotFound(ErrorCode.NewsItemNotFound);
        }

        if (!await files.ExistsAsync(request.ThumbnailId, ct))
        {
            return Error.Validation(ErrorCode.NewsItemThumbnailNotFound);
        }

        var previousThumbnailId = newsItem.ThumbnailId;
        var previousDescription = newsItem.Description;

        newsItem.Update(
            new NewsItemContent(
                request.Title,
                request.Subtitle,
                request.Description,
                request.ThumbnailId
            ),
            command.UserId,
            clock.UtcNow
        );

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.News);

        var orphanCandidates = RichTextFileReferences
            .ExtractRemoved(previousDescription, newsItem.Description)
            .ToList();
        if (previousThumbnailId != request.ThumbnailId)
        {
            orphanCandidates.Add(previousThumbnailId);
        }

        await orphanCleaner.DeleteOrphanedAsync(orphanCandidates, ct);

        return Result.Success();
    }
}
