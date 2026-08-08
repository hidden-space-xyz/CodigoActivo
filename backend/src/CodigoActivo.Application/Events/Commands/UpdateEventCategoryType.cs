using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events.Commands;

public sealed record UpdateEventCategoryTypeCommand(
    Guid CategoryTypeId,
    UpdateEventCategoryTypeRequest Request
) : ICommand<Result<EventCategoryTypeResponse>>;

public sealed class UpdateEventCategoryTypeCommandHandler(
    IEventCategoryTypeRepository categoryTypes,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UpdateEventCategoryTypeCommand, Result<EventCategoryTypeResponse>>
{
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
