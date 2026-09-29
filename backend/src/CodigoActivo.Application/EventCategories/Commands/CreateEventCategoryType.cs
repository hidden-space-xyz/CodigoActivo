using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.EventCategories.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;

namespace CodigoActivo.Application.EventCategories.Commands;

/// <summary>
/// Carries the input required to create an event category type.
/// </summary>
/// <param name="Request">Validated client request data.</param>
public sealed record CreateEventCategoryTypeCommand(CreateEventCategoryTypeRequest Request)
    : ICommand<Result<Guid>>;

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
) : ICommandHandler<CreateEventCategoryTypeCommand, Result<Guid>>
{
    /// <summary>
    /// Handles the request to create an event category type.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifier of the created item, or an application error on failure.</returns>
    public async Task<Result<Guid>> HandleAsync(
        CreateEventCategoryTypeCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var name = request.Name.Trim();
        if (await categoryTypes.NameExistsAsync(name, ct: ct))
        {
            return Error.Conflict(ErrorCode.EventCategoryTypeNameAlreadyExists);
        }

        var categoryType = EventCategoryType.Create(name, request.Color);
        await categoryTypes.AddAsync(categoryType, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.EventCategoryTypes);
        return categoryType.Id;
    }
}
