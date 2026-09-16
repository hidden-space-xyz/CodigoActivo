using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events.Commands;

/// <summary>
/// Carries the input required to update the event category type.
/// </summary>
/// <param name="CategoryTypeId">Identifier of the category type.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record UpdateEventCategoryTypeCommand(
    Guid CategoryTypeId,
    UpdateEventCategoryTypeRequest Request
) : ICommand<Result<EventCategoryTypeResponse>>;

/// <summary>
/// Executes the command to update the event category type.
/// </summary>
/// <param name="categoryTypes">Repository used to persist and retrieve category types.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class UpdateEventCategoryTypeCommandHandler(
    IEventCategoryTypeRepository categoryTypes,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UpdateEventCategoryTypeCommand, Result<EventCategoryTypeResponse>>
{
    /// <summary>
    /// Handles the request to update the event category type.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an event category type on success, or an application error on failure.</returns>
    public async Task<Result<EventCategoryTypeResponse>> HandleAsync(
        UpdateEventCategoryTypeCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var categoryType = await categoryTypes.FindAsync(x => x.Id == command.CategoryTypeId, ct);
        if (categoryType is null)
        {
            return Error.NotFound(ErrorCode.EventCategoryTypeNotFound);
        }

        var name = request.Name.Trim();
        if (
            await categoryTypes.ExistsAsync(
                x => x.Name == name && x.Id != command.CategoryTypeId,
                ct
            )
        )
        {
            return Error.Conflict(ErrorCode.EventCategoryTypeNameAlreadyExists);
        }

        categoryType.Name = name;
        categoryType.Color = request.Color.Trim();
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.EventCategoryTypes, CacheTags.Events);
        return categoryType.ToResponse();
    }
}
