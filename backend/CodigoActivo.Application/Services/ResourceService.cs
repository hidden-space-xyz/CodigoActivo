using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Services;

public class ResourceService(
    IResourceRepository resources,
    IFileRepository files,
    IUnitOfWork uow
) : IResourceService
{
    public async Task<IReadOnlyList<ResourceResponse>> ListAsync(CancellationToken ct = default)
    {
        var items = await resources.GetAllAsync(ct);
        return items.OrderByDescending(r => r.CreatedAt).Select(r => r.ToResponse()).ToList();
    }

    public async Task<Result<ResourceResponse>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        var resource = await resources.FindAsync(r => r.Id == id, ct);
        var response = resource?.ToResponse();

        return response is null ? Error.NotFound() : Result.Success(response);
    }

    public async Task<Result<ResourceResponse>> CreateAsync(
        CreateResourceRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var thumbnail = await EnsureThumbnailAsync(request.ThumbnailId, ct);
        if (thumbnail.IsFailure)
        {
            return thumbnail.Error!;
        }

        var resource = new Resource
        {
            Title = request.Title.Trim(),
            Subtitle = request.Subtitle.Trim(),
            Description = request.Description,
            ThumbnailId = request.ThumbnailId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId,
        };
        await resources.AddAsync(resource, ct);
        await uow.SaveChangesAsync(ct);
        return resource.ToResponse();
    }

    public async Task<Result<ResourceResponse>> UpdateAsync(
        Guid id,
        UpdateResourceRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var resource = await resources.FindAsync(r => r.Id == id, ct);
        if (resource is null)
        {
            return Error.NotFound();
        }

        var thumbnail = await EnsureThumbnailAsync(request.ThumbnailId, ct);
        if (thumbnail.IsFailure)
        {
            return thumbnail.Error!;
        }

        resource.Title = request.Title.Trim();
        resource.Subtitle = request.Subtitle.Trim();
        resource.Description = request.Description;
        resource.ThumbnailId = request.ThumbnailId;
        resource.UpdatedAt = DateTimeOffset.UtcNow;
        resource.UpdatedBy = userId;

        resources.Update(resource);
        await uow.SaveChangesAsync(ct);
        return resource.ToResponse();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!await resources.ExistsAsync(r => r.Id == id, ct))
        {
            return Error.NotFound();
        }

        await resources.RemoveAsync(r => r.Id == id, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> EnsureThumbnailAsync(Guid thumbnailId, CancellationToken ct)
    {
        if (!await files.ExistsAsync(f => f.Id == thumbnailId, ct))
        {
            return Error.Validation();
        }

        return Result.Success();
    }
}
