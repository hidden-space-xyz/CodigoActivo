using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Services;

public class ResourceService(
    IResourceRepository resources,
    IFileRepository files,
    IFileService fileService,
    IQueryExecutor executor,
    IClock clock,
    IUnitOfWork uow
) : IResourceService
{
    private static readonly SortMap<ResourceListItemResponse> Sort =
        new SortMap<ResourceListItemResponse>()
            .Add("createdAt", r => r.CreatedAt)
            .Add("title", r => r.Title)
            .Add("subtitle", r => r.Subtitle)
            .Default("-createdAt")
            .Tie(r => r.Id);

    public Task<PagedResult<ResourceListItemResponse>> ListAsync(
        ResourceListQuery query,
        CancellationToken ct = default
    )
    {
        var source = resources.Query().Select(Projections.ResourceListItem);

        if (!string.IsNullOrWhiteSpace(query.Title))
        {
            source = source.Where(
                TextSearch.Contains<ResourceListItemResponse>(
                    r => r.Title,
                    TextSearch.Normalize(query.Title)
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(query.Subtitle))
        {
            source = source.Where(
                TextSearch.Contains<ResourceListItemResponse>(
                    r => r.Subtitle,
                    TextSearch.Normalize(query.Subtitle)
                )
            );
        }

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }

    public async Task<Result<ResourceResponse>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        var response = await executor.FirstOrDefaultAsync(
            resources.Query().Where(r => r.Id == id).Select(Projections.Resource),
            ct
        );
        return response is null ? (Result<ResourceResponse>)Error.NotFound(ErrorCode.ResourceNotFound) : (Result<ResourceResponse>)response;
    }

    public async Task<Result<ResourceResponse>> CreateAsync(
        CreateResourceRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var thumbnail = await files.EnsureThumbnailExistsAsync(
            request.ThumbnailId,
            ErrorCode.ResourceThumbnailNotFound,
            ct
        );
        if (thumbnail.IsFailure) return thumbnail.Error!;

        var resource = new Resource
        {
            Title = request.Title.Trim(),
            Subtitle = request.Subtitle.Trim(),
            Description = request.Description,
            ThumbnailId = request.ThumbnailId,
            CreatedAt = clock.UtcNow,
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
        if (resource is null) return Error.NotFound(ErrorCode.ResourceNotFound);

        var thumbnail = await files.EnsureThumbnailExistsAsync(
            request.ThumbnailId,
            ErrorCode.ResourceThumbnailNotFound,
            ct
        );
        if (thumbnail.IsFailure) return thumbnail.Error!;

        var previousThumbnailId = resource.ThumbnailId;
        var previousDescription = resource.Description;

        resource.Title = request.Title.Trim();
        resource.Subtitle = request.Subtitle.Trim();
        resource.Description = request.Description;
        resource.ThumbnailId = request.ThumbnailId;
        resource.UpdatedAt = clock.UtcNow;
        resource.UpdatedBy = userId;

        await uow.SaveChangesAsync(ct);

        if (previousThumbnailId != request.ThumbnailId)
            await fileService.DeleteIfOrphanedAsync(previousThumbnailId, ct);

        foreach (var fileId in RichTextFileReferences.ExtractRemoved(previousDescription, resource.Description))
            await fileService.DeleteIfOrphanedAsync(fileId, ct);

        return resource.ToResponse();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var resource = await resources.FindAsync(r => r.Id == id, ct);
        if (resource is null) return Error.NotFound(ErrorCode.ResourceNotFound);

        resources.Remove(resource);
        await uow.SaveChangesAsync(ct);

        await fileService.DeleteIfOrphanedAsync(resource.ThumbnailId, ct);
        foreach (var fileId in RichTextFileReferences.Extract(resource.Description))
            await fileService.DeleteIfOrphanedAsync(fileId, ct);

        return Result.Success();
    }
}