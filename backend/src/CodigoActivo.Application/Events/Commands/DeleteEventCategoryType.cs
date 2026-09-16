using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events.Commands;

/// <summary>
/// Carries the input required to delete the event category type.
/// </summary>
/// <param name="CategoryTypeId">Identifier of the category type.</param>
public sealed record DeleteEventCategoryTypeCommand(Guid CategoryTypeId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the event category type.
/// </summary>
/// <param name="categoryTypes">Repository used to persist and retrieve category types.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class DeleteEventCategoryTypeCommandHandler(
    IEventCategoryTypeRepository categoryTypes,
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
        if (await categoryTypes.RemoveAsync(x => x.Id == command.CategoryTypeId, ct) is 0)
        {
            return Error.NotFound(ErrorCode.EventCategoryTypeNotFound);
        }

        await cacheInvalidator.InvalidateAsync(CacheTags.EventCategoryTypes, CacheTags.Events);
        return Result.Success();
    }
}
