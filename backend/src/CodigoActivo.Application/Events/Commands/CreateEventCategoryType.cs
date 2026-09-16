using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events.Commands;

/// <summary>
/// Carries the input required to create an event category type.
/// </summary>
/// <param name="Request">Validated client request data.</param>
public sealed record CreateEventCategoryTypeCommand(CreateEventCategoryTypeRequest Request)
    : ICommand<Result<EventCategoryTypeResponse>>;

/// <summary>
/// Executes the command to create an event category type.
/// </summary>
/// <param name="categoryTypes">Repository used to persist and retrieve category types.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class CreateEventCategoryTypeCommandHandler(
    IEventCategoryTypeRepository categoryTypes,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<CreateEventCategoryTypeCommand, Result<EventCategoryTypeResponse>>
{
    /// <summary>
    /// Handles the request to create an event category type.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an event category type on success, or an application error on failure.</returns>
    public async Task<Result<EventCategoryTypeResponse>> HandleAsync(
        CreateEventCategoryTypeCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var name = request.Name.Trim();
        if (await categoryTypes.ExistsAsync(x => x.Name == name, ct))
        {
            return Error.Conflict(ErrorCode.EventCategoryTypeNameAlreadyExists);
        }

        var categoryType = new EventCategoryType { Name = name, Color = request.Color.Trim() };
        await categoryTypes.AddAsync(categoryType, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.EventCategoryTypes);
        return categoryType.ToResponse();
    }
}
