using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.News;

namespace CodigoActivo.Application.News.Commands;

/// <summary>
/// Carries the input required to delete the news item.
/// </summary>
/// <param name="NewsItemId">Identifier of the news item.</param>
public sealed record DeleteNewsItemCommand(NewsItemId NewsItemId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the news item. Its thumbnail and body images are removed after
/// the commit when nothing else uses them.
/// </summary>
/// <param name="news">Repository used to persist and retrieve news.</param>
public sealed class DeleteNewsItemCommandHandler(INewsItemRepository news)
    : ICommandHandler<DeleteNewsItemCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the news item.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success, or <see cref="ApplicationErrorCode.NewsItemNotFound"/>.
    /// </returns>
    public async Task<Result> HandleAsync(
        DeleteNewsItemCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var newsItem = await news.GetByIdAsync(command.NewsItemId, ct);
        if (newsItem is null)
        {
            return Error.NotFound(ApplicationErrorCode.NewsItemNotFound);
        }

        newsItem.Delete();
        news.Remove(newsItem);
        return Result.Success();
    }
}
