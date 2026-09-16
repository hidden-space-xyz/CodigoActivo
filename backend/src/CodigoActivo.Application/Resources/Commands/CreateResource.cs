using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Resources.Commands;

/// <summary>
/// Carries the input required to create a resource.
/// </summary>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record CreateResourceCommand(CreateResourceRequest Request, Guid UserId)
    : ICommand<Result<ResourceResponse>>;

/// <summary>
/// Executes the command to create a resource.
/// </summary>
/// <param name="resources">Repository used to persist and retrieve resources.</param>
/// <param name="resourceTypes">Repository used to persist and retrieve resource types.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class CreateResourceCommandHandler(
    IResourceRepository resources,
    IResourceTypeRepository resourceTypes,
    IFileRepository files,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<CreateResourceCommand, Result<ResourceResponse>>
{
    /// <summary>
    /// Handles the request to create a resource.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a resource on success, or an application error on failure.</returns>
    public async Task<Result<ResourceResponse>> HandleAsync(
        CreateResourceCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

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

        var resource = new Resource
        {
            Title = request.Title.Trim(),
            Subtitle = request.Subtitle.Trim(),
            Description = content.Value.Description,
            Url = content.Value.Url,
            ResourceTypeId = type.Id,
            ResourceType = type,
            ThumbnailId = request.ThumbnailId,
            CreatedAt = clock.UtcNow,
            CreatedBy = command.UserId,
        };
        await resources.AddAsync(resource, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Resources);
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
