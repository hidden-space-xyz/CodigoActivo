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
/// Carries the input required to update the news item.
/// </summary>
/// <param name="NewsItemId">Identifier of the news item.</param>
/// <param name="Title">Headline, up to 200 characters.</param>
/// <param name="Subtitle">Line shown below the headline, up to 300 characters.</param>
/// <param name="Description">Body as rich text JSON within the rich text allowlist.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file, which must exist.</param>
public sealed record UpdateNewsItemCommand(
    NewsItemId NewsItemId,
    [property: Required, MaxLength(200), NotBlank] string Title,
    [property: Required, MaxLength(300), NotBlank] string Subtitle,
    [property: RichText, MaxLength(RichTextAllowlist.MaxJsonLength)] string Description,
    StoredFileId ThumbnailId
) : ICommand<Result>;

/// <summary>
/// Executes the command to update the news item. A thumbnail or body image it no longer references
/// is removed after the commit when nothing else uses it.
/// </summary>
/// <param name="news">Repository used to persist and retrieve news.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="currentUser">Person the use case runs for, recorded as the editor.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work that saves the item in the transaction that locks its files.</param>
public sealed class UpdateNewsItemCommandHandler(
    INewsItemRepository news,
    IStoredFileRepository files,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork uow
) : ICommandHandler<UpdateNewsItemCommand, Result>
{
    /// <summary>
    /// Handles the request to update the news item.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success; <see cref="ApplicationErrorCode.NewsItemNotFound"/>, or
    /// <see cref="ApplicationErrorCode.NewsItemThumbnailNotFound"/> or
    /// <see cref="ApplicationErrorCode.RichTextFileNotFound"/> when a file is missing.
    /// </returns>
    public async Task<Result> HandleAsync(
        UpdateNewsItemCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var newsItem = await news.GetByIdAsync(command.NewsItemId, ct);
        if (newsItem is null)
        {
            return Error.NotFound(ApplicationErrorCode.NewsItemNotFound);
        }

        return await uow.ExecuteInTransactionAsync(
            attempt => UpdateLockedAsync(newsItem, command, attempt),
            ct
        );
    }

    private async Task<Result> UpdateLockedAsync(
        NewsItem newsItem,
        UpdateNewsItemCommand command,
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
            return available;
        }

        newsItem.Update(
            new NewsItemContent(command.Title, command.Subtitle, description, command.ThumbnailId),
            currentUser.RequiredId(),
            clock.UtcNow
        );
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
