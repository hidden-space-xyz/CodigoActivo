using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events.Commands;

public sealed record CreateEventCategoryTypeCommand(CreateEventCategoryTypeRequest Request)
    : ICommand<Result<EventCategoryTypeResponse>>;

public sealed class CreateEventCategoryTypeCommandHandler(
    IEventCategoryTypeRepository categoryTypes,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<CreateEventCategoryTypeCommand, Result<EventCategoryTypeResponse>>
{
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
