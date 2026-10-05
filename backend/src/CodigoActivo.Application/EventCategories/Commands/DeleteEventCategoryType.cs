using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.EventCategories.Commands;

/// <summary>
/// Carries the input required to delete the event category type.
/// </summary>
/// <param name="CategoryTypeId">Identifier of the category type.</param>
public sealed record DeleteEventCategoryTypeCommand(Guid CategoryTypeId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the event category type. The events it tagged lose the tag, so it
/// is refused while some event has no other category.
/// </summary>
/// <param name="categoryTypes">Repository used to persist and retrieve category types.</param>
/// <param name="events">Repository used to check which events would be left without a category.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class DeleteEventCategoryTypeCommandHandler(
    IEventCategoryTypeRepository categoryTypes,
    IEventRepository events,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeleteEventCategoryTypeCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the event category type.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        DeleteEventCategoryTypeCommand command,
        CancellationToken ct = default
    )
    {
        var categoryType = await categoryTypes.GetByIdAsync(command.CategoryTypeId, ct);
        if (categoryType is null)
        {
            return Error.NotFound(ErrorCode.EventCategoryTypeNotFound);
        }

        if (await events.HasEventWithOnlyCategoryAsync(categoryType.Id, ct))
        {
            return Error.Conflict(ErrorCode.EventCategoryTypeOnlyCategoryOfEvent);
        }

        categoryTypes.Remove(categoryType);
        await uow.SaveChangesAsync(ct);

        await cacheInvalidator.InvalidateAsync(CacheTags.EventCategoryTypes, CacheTags.Events);
        return Result.Success();
    }
}
