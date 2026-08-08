using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events.Commands;

public sealed record DeleteEventCategoryTypeCommand(Guid CategoryTypeId) : ICommand<Result>;

public sealed class DeleteEventCategoryTypeCommandHandler(
    IEventCategoryTypeRepository categoryTypes,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeleteEventCategoryTypeCommand, Result>
{
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
