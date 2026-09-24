using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

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
) : ICommand<Result<NewsItemResponse>>;

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
    IFileRepository files,
    IOrphanFileCleaner orphanCleaner,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UpdateNewsItemCommand, Result<NewsItemResponse>>
{
    /// <summary>
    /// Handles the request to update the news item.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a news item on success, or an application error on failure.</returns>
    public async Task<Result<NewsItemResponse>> HandleAsync(
        UpdateNewsItemCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var newsItem = await news.FindAsync(a => a.Id == command.NewsItemId, ct);
        if (newsItem is null)
        {
            return Error.NotFound(ErrorCode.NewsItemNotFound);
        }

        if (!await files.ExistsAsync(f => f.Id == request.ThumbnailId, ct))
        {
            return Error.BadRequest(ErrorCode.NewsItemThumbnailNotFound);
        }

        var previousThumbnailId = newsItem.ThumbnailId;
        var previousDescription = newsItem.Description;

        newsItem.Title = request.Title.Trim();
        newsItem.Subtitle = request.Subtitle.Trim();
        newsItem.Description = request.Description;
        newsItem.ThumbnailId = request.ThumbnailId;
        newsItem.UpdatedAt = clock.UtcNow;
        newsItem.UpdatedBy = command.UserId;

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

        return newsItem.ToResponse();
    }
}
