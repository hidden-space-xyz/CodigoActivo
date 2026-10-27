using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.News;

namespace CodigoActivo.Application.News.Commands;

/// <summary>
/// Carries the input required to make a news item the featured one.
/// </summary>
/// <param name="NewsItemId">Identifier of the news item to feature.</param>
public sealed record SetNewsItemFeaturedCommand(NewsItemId NewsItemId) : ICommand<Result>;

/// <summary>
/// Executes the command to make a news item the featured one: every other featured item stops
/// being featured first, in the same transaction, so at most one item is featured at a time.
/// </summary>
/// <param name="news">Repository used to persist and retrieve news items.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class SetNewsItemFeaturedCommandHandler(INewsItemRepository news, IUnitOfWork uow)
    : ICommandHandler<SetNewsItemFeaturedCommand, Result>
{
    /// <summary>
    /// Handles the request to make a news item the featured one.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success, or <see cref="ApplicationErrorCode.NewsItemNotFound"/>.
    /// </returns>
    public async Task<Result> HandleAsync(
        SetNewsItemFeaturedCommand command,
        CancellationToken ct = default
    )
    {
        var chosen = await news.GetByIdAsync(command.NewsItemId, ct);
        if (chosen is null)
        {
            return Error.NotFound(ApplicationErrorCode.NewsItemNotFound);
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

        return Result.Success();
    }
}
