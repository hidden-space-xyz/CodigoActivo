using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;

namespace CodigoActivo.Application.News.Commands;

/// <summary>
/// Carries the input required to create a news item.
/// </summary>
/// <param name="Title">Headline, up to 200 characters.</param>
/// <param name="Subtitle">Line shown below the headline, up to 300 characters.</param>
/// <param name="Description">Body as rich text JSON within the rich text allowlist.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file, which must exist.</param>
public sealed record CreateNewsItemCommand(
    [property: Required, MaxLength(200), NotBlank] string Title,
    [property: Required, MaxLength(300), NotBlank] string Subtitle,
    [property: RichText, MaxLength(RichTextAllowlist.MaxJsonLength)] string Description,
    StoredFileId ThumbnailId
) : ICommand<Result<NewsItemId>>;

/// <summary>
/// Executes the command to create a news item.
/// </summary>
/// <param name="news">Repository used to persist and retrieve news.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="currentUser">Person the use case runs for, recorded as the creator.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work that saves the item in the transaction that locks its files.</param>
public sealed class CreateNewsItemCommandHandler(
    INewsItemRepository news,
    IStoredFileRepository files,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork uow
) : ICommandHandler<CreateNewsItemCommand, Result<NewsItemId>>
{
    /// <summary>
    /// Handles the request to create a news item.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the identifier of the new item;
    /// <see cref="ApplicationErrorCode.NewsItemThumbnailNotFound"/> or
    /// <see cref="ApplicationErrorCode.RichTextFileNotFound"/> when a file is missing.
    /// </returns>
    public async Task<Result<NewsItemId>> HandleAsync(
        CreateNewsItemCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        return await uow.ExecuteInTransactionAsync(
            attempt => CreateLockedAsync(command, attempt),
            ct
        );
    }

    private async Task<Result<NewsItemId>> CreateLockedAsync(
        CreateNewsItemCommand command,
        CancellationToken ct
    )
    {
        var description = RichText.From(command.Description);
        var available = await ContentFiles.EnsureAvailableAsync(
            files,
            command.ThumbnailId,
            description,
            ApplicationErrorCode.NewsItemThumbnailNotFound,
            ct
        );
        if (available.IsFailure)
        {
            return available.Error!;
        }

        var newsItem = NewsItem.Create(
            new NewsItemContent(command.Title, command.Subtitle, description, command.ThumbnailId),
            currentUser.RequiredId(),
            clock.UtcNow
        );
        await news.AddAsync(newsItem, ct);
        await uow.SaveChangesAsync(ct);
        return newsItem.Id;
    }
}
