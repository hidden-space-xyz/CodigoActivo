using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.EventCategories.Commands;

/// <summary>
/// Carries the input required to delete the event category type.
/// </summary>
/// <param name="CategoryTypeId">Identifier of the category type.</param>
public sealed record DeleteEventCategoryTypeCommand(EventCategoryTypeId CategoryTypeId)
    : ICommand<Result>;

/// <summary>
/// Executes the command to delete the event category type. Each event tagged with it loses the
/// category through <see cref="Event.RemoveCategory"/>, which refuses to leave an event without
/// any. The category and the events tagged with it stay locked meanwhile, so neither tagging
/// another event with it nor changing the categories of those events, which locks them too, can
/// slip in between.
/// </summary>
/// <param name="categoryTypes">Repository used to persist and retrieve category types.</param>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="uow">Unit of work that runs the deletion in one transaction.</param>
public sealed class DeleteEventCategoryTypeCommandHandler(
    IEventCategoryTypeRepository categoryTypes,
    IEventRepository events,
    IUnitOfWork uow
) : ICommandHandler<DeleteEventCategoryTypeCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the event category type.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the error.</returns>
    public async Task<Result> HandleAsync(
        DeleteEventCategoryTypeCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var categoryType = await categoryTypes.GetByIdAsync(command.CategoryTypeId, ct);
        if (categoryType is null)
        {
            return Error.NotFound(ApplicationErrorCode.EventCategoryTypeNotFound);
        }

        return await uow.ExecuteInTransactionAsync(
            attempt => DeleteLockedAsync(categoryType, attempt),
            ct
        );
    }

    private async Task<Result> DeleteLockedAsync(
        EventCategoryType categoryType,
        CancellationToken ct
    )
    {
        if (!await categoryTypes.LockAsync(categoryType, ct))
        {
            return Error.NotFound(ApplicationErrorCode.EventCategoryTypeNotFound);
        }

        foreach (var tagged in await events.LockTaggedWithAsync(categoryType.Id, ct))
        {
            var removed = tagged.RemoveCategory(categoryType.Id);
            if (removed.IsFailure)
            {
                return removed;
            }
        }

        categoryType.Delete();
        categoryTypes.Remove(categoryType);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
