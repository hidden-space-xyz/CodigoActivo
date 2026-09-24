using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.News.Commands;

/// <summary>
/// Carries the input required to create a news item.
/// </summary>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record CreateNewsItemCommand(CreateNewsItemRequest Request, Guid UserId)
    : ICommand<Result<NewsItemResponse>>;

/// <summary>
/// Executes the command to create a news item.
/// </summary>
/// <param name="news">Repository used to persist and retrieve news items.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class CreateNewsItemCommandHandler(
    INewsItemRepository news,
    IFileRepository files,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<CreateNewsItemCommand, Result<NewsItemResponse>>
{
    /// <summary>
    /// Handles the request to create a news item.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a news item on success, or an application error on failure.</returns>
    public async Task<Result<NewsItemResponse>> HandleAsync(
        CreateNewsItemCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        if (!await files.ExistsAsync(f => f.Id == request.ThumbnailId, ct))
        {
            return Error.BadRequest(ErrorCode.NewsItemThumbnailNotFound);
        }

        var newsItem = new NewsItem
        {
            Title = request.Title.Trim(),
            Subtitle = request.Subtitle.Trim(),
            Description = request.Description,
            ThumbnailId = request.ThumbnailId,
            CreatedAt = clock.UtcNow,
            CreatedBy = command.UserId,
        };
        await news.AddAsync(newsItem, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.News);
        return newsItem.ToResponse();
    }
}
