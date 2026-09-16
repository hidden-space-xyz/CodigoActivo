using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Resources.Commands;

/// <summary>
/// Carries the input required to delete the resource.
/// </summary>
/// <param name="ResourceId">Identifier of the resource.</param>
public sealed record DeleteResourceCommand(Guid ResourceId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the resource.
/// </summary>
/// <param name="resources">Repository used to persist and retrieve resources.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class DeleteResourceCommandHandler(
    IResourceRepository resources,
    IOrphanFileCleaner orphanCleaner,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeleteResourceCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the resource.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
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
