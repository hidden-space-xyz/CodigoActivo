using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

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
) : ICommand<Result<ResourceResponse>>;

/// <summary>
/// Executes the command to update the resource.
/// </summary>
/// <param name="resources">Repository used to persist and retrieve resources.</param>
/// <param name="resourceTypes">Repository used to persist and retrieve resource types.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class UpdateResourceCommandHandler(
    IResourceRepository resources,
    IResourceTypeRepository resourceTypes,
    IFileRepository files,
    IOrphanFileCleaner orphanCleaner,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UpdateResourceCommand, Result<ResourceResponse>>
{
    /// <summary>
    /// Handles the request to update the resource.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a resource on success, or an application error on failure.</returns>
    public async Task<Result<ResourceResponse>> HandleAsync(
        UpdateResourceCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var resource = await resources.FindAsync(r => r.Id == command.ResourceId, ct);
        if (resource is null)
        {
            return Error.NotFound(ErrorCode.ResourceNotFound);
        }

        var type = await resourceTypes.FindAsync(t => t.Id == request.ResourceTypeId, ct);
        if (type is null)
        {
            return Error.BadRequest(ErrorCode.ResourceTypeNotFound);
        }

        var content = ResolveContent(type, request.Description, request.Url);
        if (content.IsFailure)
        {
            return content.Error!;
        }

        if (!await files.ExistsAsync(f => f.Id == request.ThumbnailId, ct))
        {
            return Error.BadRequest(ErrorCode.ResourceThumbnailNotFound);
        }

        var previousThumbnailId = resource.ThumbnailId;
        var previousDescription = resource.Description;

        resource.Title = request.Title.Trim();
        resource.Subtitle = request.Subtitle.Trim();
        resource.Description = content.Value.Description;
        resource.Url = content.Value.Url;
        resource.ResourceTypeId = type.Id;
        resource.ResourceType = type;
        resource.ThumbnailId = request.ThumbnailId;
        resource.UpdatedAt = clock.UtcNow;
        resource.UpdatedBy = command.UserId;

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

        return resource.ToResponse();
    }

    private static Result<(string Description, string? Url)> ResolveContent(
        ResourceType type,
        string? description,
        string? url
    )
    {
        if (type.IsExternal)
        {
            if (!RichTextDocument.IsEmpty(description))
            {
                return Error.BadRequest(ErrorCode.ResourceDescriptionNotAllowed);
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                return Error.BadRequest(ErrorCode.ResourceUrlRequired);
            }

            return ("{}", url.Trim());
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            return Error.BadRequest(ErrorCode.ResourceUrlNotAllowed);
        }

        if (RichTextDocument.IsEmpty(description))
        {
            return Error.BadRequest(ErrorCode.ResourceDescriptionRequired);
        }

        return (description!, null);
    }
}
