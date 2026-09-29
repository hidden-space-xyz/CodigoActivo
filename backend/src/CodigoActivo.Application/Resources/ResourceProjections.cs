using System.Linq.Expressions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Resources.Contracts;

namespace CodigoActivo.Application.Resources;

/// <summary>
/// Database projections from the read models to the response shapes of this feature.
/// </summary>
public static class ResourceProjections
{
    /// <summary>
    /// Stores the shared resource value.
    /// </summary>
    public static readonly Expression<Func<ResourceRow, ResourceResponse>> Resource =
        resource => new ResourceResponse
        {
            Id = resource.Id,
            Title = resource.Title,
            Subtitle = resource.Subtitle,
            Description = resource.Description,
            Url = resource.Url,
            Type = new ResourceTypeResponse
            {
                Id = resource.ResourceType.Id,
                Name = resource.ResourceType.Name,
                Description = resource.ResourceType.Description,
                Color = resource.ResourceType.Color,
                IsExternal = resource.ResourceType.IsExternal,
            },
            CreatedAt = resource.CreatedAt,
            UpdatedAt = resource.UpdatedAt,
            CreatedBy = resource.CreatedBy,
            UpdatedBy = resource.UpdatedBy,
            ThumbnailId = resource.ThumbnailId,
        };

    /// <summary>
    /// Stores the shared resource list item value.
    /// </summary>
    public static readonly Expression<
        Func<ResourceRow, ResourceListItemResponse>
    > ResourceListItem = resource => new ResourceListItemResponse
    {
        Id = resource.Id,
        Title = resource.Title,
        Subtitle = resource.Subtitle,
        Url = resource.Url,
        Type = new ResourceTypeResponse
        {
            Id = resource.ResourceType.Id,
            Name = resource.ResourceType.Name,
            Description = resource.ResourceType.Description,
            Color = resource.ResourceType.Color,
            IsExternal = resource.ResourceType.IsExternal,
        },
        CreatedAt = resource.CreatedAt,
        UpdatedAt = resource.UpdatedAt,
        CreatedBy = resource.CreatedBy,
        UpdatedBy = resource.UpdatedBy,
        ThumbnailId = resource.ThumbnailId,
    };

    /// <summary>
    /// Stores the shared resource type value.
    /// </summary>
    public static readonly Expression<Func<ResourceTypeRow, ResourceTypeResponse>> ResourceType =
        resourceType => new ResourceTypeResponse
        {
            Id = resourceType.Id,
            Name = resourceType.Name,
            Description = resourceType.Description,
            Color = resourceType.Color,
            IsExternal = resourceType.IsExternal,
        };
}
