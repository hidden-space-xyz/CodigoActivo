using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Resources.Commands;

public sealed record CreateResourceCommand(CreateResourceRequest Request, Guid UserId)
    : ICommand<Result<ResourceResponse>>;

public sealed class CreateResourceCommandHandler(
    IResourceRepository resources,
    IResourceTypeRepository resourceTypes,
    IFileRepository files,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<CreateResourceCommand, Result<ResourceResponse>>
{
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
