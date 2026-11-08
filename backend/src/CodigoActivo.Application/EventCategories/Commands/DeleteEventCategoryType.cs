using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
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
    : ICommand<Result>,
        IRoutineCommand;

/// <summary>
/// Executes the command to delete the event category type. Each event tagged with it loses the
/// category through <see cref="Event.RemoveCategory"/>, which refuses to leave an event without
/// any.
/// </summary>
/// <param name="categoryTypes">Repository used to persist and retrieve category types.</param>
/// <param name="events">Repository used to persist and retrieve events.</param>
public sealed class DeleteEventCategoryTypeCommandHandler(
    IEventCategoryTypeRepository categoryTypes,
    IEventRepository events
) : ICommandHandler<DeleteEventCategoryTypeCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the event category type.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success;
    /// <see cref="ApplicationErrorCode.EventCategoryTypeNotFound"/>, or
    /// <see cref="DomainErrorCode.EventCategoryTypeOnlyCategoryOfEvent"/> when it is the only
    /// category of an event, in which case nothing is deleted.
    /// </returns>
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

        var taggedEvents = await events.ListTaggedWithAsync(categoryType.Id, ct);
        foreach (
            var removed in taggedEvents.Select(tagged => tagged.RemoveCategory(categoryType.Id))
        )
        {
            if (removed.IsFailure)
            {
                return removed;
            }
        }

        categoryType.Delete();
        categoryTypes.Remove(categoryType);
        return Result.Success();
    }
}
