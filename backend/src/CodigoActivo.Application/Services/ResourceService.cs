using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Services;

public class ResourceService(
    IResourceRepository resources,
    IResourceTypeRepository resourceTypes,
    IFileRepository files,
    IFileService fileService,
    IQueryExecutor executor,
    IClock clock,
    IUnitOfWork uow,
    HybridCache cache,
    ICacheInvalidator cacheInvalidator
) : IResourceService
{
    private static readonly SortMap<ResourceListItemResponse> Sort =
        new SortMap<ResourceListItemResponse>()
            .Add("createdAt", r => r.CreatedAt)
            .Add("title", r => r.Title)
            .Add("subtitle", r => r.Subtitle)
            .Add("type", r => r.Type.Name)
            .Add("url", r => r.Url)
            .Default("-createdAt")
            .Tie(r => r.Id);

    public async Task<PagedResult<ResourceListItemResponse>> ListAsync(
        ResourceListQuery query,
        CancellationToken ct = default
    )
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.For("resources:list", query),
            token => new ValueTask<PagedResult<ResourceListItemResponse>>(
                FetchListAsync(query, token)
            ),
            CachePolicies.PublicContent,
            [CacheTags.Resources],
            ct
        );
    }

    private Task<PagedResult<ResourceListItemResponse>> FetchListAsync(
        ResourceListQuery query,
        CancellationToken ct
    )
    {
        var source = resources.Query().Select(Projections.ResourceListItem);

        if (query.ResourceTypeId is { } resourceTypeId)
            source = source.Where(r => r.Type.Id == resourceTypeId);
        if (query.CreatedFrom is { } createdFrom)
        {
            var createdLower = LocalDayRange.LowerUtc(createdFrom, clock.TimeZone);
            source = source.Where(r => r.CreatedAt >= createdLower);
        }

        if (query.CreatedTo is { } createdTo)
        {
            var createdUpper = LocalDayRange.UpperExclusiveUtc(createdTo, clock.TimeZone);
            source = source.Where(r => r.CreatedAt < createdUpper);
        }

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

        if (!string.IsNullOrWhiteSpace(query.Url))
        {
            source = source.Where(
                TextSearch.Contains<ResourceListItemResponse>(
                    r => r.Url,
                    TextSearch.Normalize(query.Url)
                )
            );
        }

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }

    public async Task<IReadOnlyList<ResourceTypeResponse>> ListTypesAsync(
        CancellationToken ct = default
    )
    {
        return await cache.GetOrCreateAsync(
            "resources:types",
            token => new ValueTask<IReadOnlyList<ResourceTypeResponse>>(
                executor.ToListAsync(
                    resourceTypes
                        .Query()
                        .OrderBy(type => type.Name)
                        .Select(Projections.ResourceType),
                    token
                )
            ),
            CachePolicies.Catalog,
            [CacheTags.Catalogs],
            ct
        );
    }

    public async Task<Result<ResourceResponse>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        var response = await cache.GetOrCreateAsync(
            $"resources:id:{id}",
            token => new ValueTask<ResourceResponse?>(
                executor.FirstOrDefaultAsync(
                    resources.Query().Where(r => r.Id == id).Select(Projections.Resource),
                    token
                )
            ),
            CachePolicies.PublicContent,
            [CacheTags.Resources],
            ct
        );
        return response is null
            ? (Result<ResourceResponse>)Error.NotFound(ErrorCode.ResourceNotFound)
            : (Result<ResourceResponse>)response;
    }

    public async Task<Result<ResourceResponse>> CreateAsync(
        CreateResourceRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var type = await resourceTypes.FindAsync(t => t.Id == request.ResourceTypeId, ct);
        if (type is null)
            return Error.BadRequest(ErrorCode.ResourceTypeNotFound);

        var content = ResolveContent(type, request.Description, request.Url);
        if (content.IsFailure)
            return content.Error!;

        var thumbnail = await files.EnsureThumbnailExistsAsync(
            request.ThumbnailId,
            ErrorCode.ResourceThumbnailNotFound,
            ct
        );
        if (thumbnail.IsFailure)
            return thumbnail.Error!;

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
            CreatedBy = userId,
        };
        await resources.AddAsync(resource, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Resources);
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
            return Error.NotFound(ErrorCode.ResourceNotFound);

        var type = await resourceTypes.FindAsync(t => t.Id == request.ResourceTypeId, ct);
        if (type is null)
            return Error.BadRequest(ErrorCode.ResourceTypeNotFound);

        var content = ResolveContent(type, request.Description, request.Url);
        if (content.IsFailure)
            return content.Error!;

        var thumbnail = await files.EnsureThumbnailExistsAsync(
            request.ThumbnailId,
            ErrorCode.ResourceThumbnailNotFound,
            ct
        );
        if (thumbnail.IsFailure)
            return thumbnail.Error!;

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
        resource.UpdatedBy = userId;

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Resources);

        var orphanCandidates = RichTextFileReferences
            .ExtractRemoved(previousDescription, resource.Description)
            .ToList();
        if (previousThumbnailId != request.ThumbnailId)
            orphanCandidates.Add(previousThumbnailId);
        await fileService.DeleteOrphanedAsync(orphanCandidates, ct);

        return resource.ToResponse();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var resource = await resources.FindAsync(r => r.Id == id, ct);
        if (resource is null)
            return Error.NotFound(ErrorCode.ResourceNotFound);

        resources.Remove(resource);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Resources);

        var orphanCandidates = RichTextFileReferences
            .Extract(resource.Description)
            .Append(resource.ThumbnailId)
            .Distinct()
            .ToList();
        await fileService.DeleteOrphanedAsync(orphanCandidates, ct);

        return Result.Success();
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
                return Error.BadRequest(ErrorCode.ResourceDescriptionNotAllowed);
            if (string.IsNullOrWhiteSpace(url))
                return Error.BadRequest(ErrorCode.ResourceUrlRequired);
            return ("{}", url.Trim());
        }

        if (!string.IsNullOrWhiteSpace(url))
            return Error.BadRequest(ErrorCode.ResourceUrlNotAllowed);
        if (RichTextDocument.IsEmpty(description))
            return Error.BadRequest(ErrorCode.ResourceDescriptionRequired);
        return (description!, null);
    }
}
