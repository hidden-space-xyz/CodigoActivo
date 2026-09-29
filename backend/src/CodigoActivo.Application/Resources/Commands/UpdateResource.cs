using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Resources.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Resources;

namespace CodigoActivo.Application.Resources.Commands;

/// <summary>
/// Carries the input required to update the resource.
/// </summary>
/// <param name="ResourceId">Identifier of the resource.</param>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record UpdateResourceCommand(
    Guid ResourceId,
    UpdateResourceRequest Request,
    Guid UserId
) : ICommand<Result>;

/// <summary>
/// Executes the command to update the resource.
/// </summary>
/// <param name="resources">Repository used to persist and retrieve resources.</param>
/// <param name="readStore">Read side used to check the resource type catalog.</param>
/// <param name="executor">Executor of the read-side queries.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class UpdateResourceCommandHandler(
    IResourceRepository resources,
    IReadStore readStore,
    IQueryExecutor executor,
    IFileRepository files,
    IOrphanFileCleaner orphanCleaner,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UpdateResourceCommand, Result>
{
    /// <summary>
    /// Handles the request to update the resource.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        UpdateResourceCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var resource = await resources.GetByIdAsync(command.ResourceId, ct);
        if (resource is null)
        {
            return Error.NotFound(ErrorCode.ResourceNotFound);
        }

        var isExternal = await executor.FirstOrDefaultAsync(
            readStore
                .ResourceTypes.Where(type => type.Id == request.ResourceTypeId)
                .Select(type => (bool?)type.IsExternal),
            ct
        );
        if (isExternal is null)
        {
            return Error.Validation(ErrorCode.ResourceTypeNotFound);
        }

        var content = ResourceContent.For(isExternal.Value, request.Description, request.Url);
        if (content.IsFailure)
        {
            return content.Error!;
        }

        if (!await files.ExistsAsync(request.ThumbnailId, ct))
        {
            return Error.Validation(ErrorCode.ResourceThumbnailNotFound);
        }

        var previousThumbnailId = resource.ThumbnailId;
        var previousDescription = resource.Description;

        resource.Update(
            new ResourceDetails(
                request.Title,
                request.Subtitle,
                request.ResourceTypeId,
                request.ThumbnailId
            ),
            content.Value,
            command.UserId,
            clock.UtcNow
        );

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Resources);

        var orphanCandidates = RichTextFileReferences
            .ExtractRemoved(previousDescription, resource.Description)
            .ToList();
        if (previousThumbnailId != request.ThumbnailId)
        {
            orphanCandidates.Add(previousThumbnailId);
        }

        await orphanCleaner.DeleteOrphanedAsync(orphanCandidates, ct);

        return Result.Success();
    }
}
