using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;

namespace CodigoActivo.Application.News.Commands;

/// <summary>
/// Carries the input required to create a news item.
/// </summary>
/// <param name="Title">Headline.</param>
/// <param name="Subtitle">Line shown below the headline.</param>
/// <param name="Description">Body as rich text JSON.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
public sealed record CreateNewsItemCommand(
    [property: Required, MaxLength(200), NotBlank] string Title,
    [property: Required, MaxLength(300), NotBlank] string Subtitle,
    [property: RichText, MaxLength(262144)] string Description,
    StoredFileId ThumbnailId
) : ICommand<Result<NewsItemId>>;

/// <summary>
/// Executes the command to create a news item.
/// </summary>
/// <param name="news">Repository used to persist and retrieve news.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class CreateNewsItemCommandHandler(
    INewsItemRepository news,
    IStoredFileRepository files,
    ICurrentUser currentUser,
    IClock clock
) : ICommandHandler<CreateNewsItemCommand, Result<NewsItemId>>
{
    /// <summary>
    /// Handles the request to create a news item.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifier of the created item, or an application error on failure.</returns>
    public async Task<Result<NewsItemId>> HandleAsync(
        CreateNewsItemCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await files.ExistsAsync(command.ThumbnailId, ct))
        {
            return Error.Validation(ApplicationErrorCode.NewsItemThumbnailNotFound);
        }

        var newsItem = NewsItem.Create(
            new NewsItemContent(
                command.Title,
                command.Subtitle,
                RichText.From(command.Description),
                command.ThumbnailId
            ),
            currentUser.RequiredId(),
            clock.UtcNow
        );
        await news.AddAsync(newsItem, ct);
        return newsItem.Id;
    }
}
