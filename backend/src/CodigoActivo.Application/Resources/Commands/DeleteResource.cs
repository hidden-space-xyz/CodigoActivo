using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Resources.Commands;

public sealed record DeleteResourceCommand(Guid ResourceId) : ICommand<Result>;

public sealed class DeleteResourceCommandHandler(
    IResourceRepository resources,
    IOrphanFileCleaner orphanCleaner,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeleteResourceCommand, Result>
{
    public async Task<Result> HandleAsync(
        DeleteResourceCommand command,
        CancellationToken ct = default
    )
    {
        var resource = await resources.FindAsync(r => r.Id == command.ResourceId, ct);
        if (resource is null)
        {
            return Error.NotFound(ErrorCode.ResourceNotFound);
        }

        resources.Remove(resource);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Resources);

        var orphanCandidates = RichTextFileReferences
            .Extract(resource.Description)
            .Append(resource.ThumbnailId)
            .Distinct()
            .ToList();
        await orphanCleaner.DeleteOrphanedAsync(orphanCandidates, ct);

        return Result.Success();
    }
}
