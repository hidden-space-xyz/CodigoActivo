using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Resources.Commands;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Resources;

namespace CodigoActivo.API.Resources.Contracts;

/// <summary>
/// Contains the client-supplied data used to create a resource.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Url">The url value.</param>
/// <param name="ResourceTypeId">Identifier of the resource type.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
public record CreateResourceRequest(
    [Required] [MaxLength(200)] string Title,
    [Required] [MaxLength(300)] string Subtitle,
    [MaxLength(262144)] string? Description,
    [MaxLength(500)] string? Url,
    Guid ResourceTypeId,
    Guid ThumbnailId
)
{
    /// <summary>
    /// Builds the command that creates the resource.
    /// </summary>
    /// <returns>The command.</returns>
    public CreateResourceCommand ToCommand()
    {
        return new CreateResourceCommand(
            Title,
            Subtitle,
            Description,
            Url,
            ResourceTypeId,
            StoredFileId.From(ThumbnailId)
        );
    }
}

/// <summary>
/// Contains the client-supplied data used to update the resource.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Url">The url value.</param>
/// <param name="ResourceTypeId">Identifier of the resource type.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
public record UpdateResourceRequest(
    [Required] [MaxLength(200)] string Title,
    [Required] [MaxLength(300)] string Subtitle,
    [MaxLength(262144)] string? Description,
    [MaxLength(500)] string? Url,
    Guid ResourceTypeId,
    Guid ThumbnailId
)
{
    /// <summary>
    /// Builds the command that updates the resource.
    /// </summary>
    /// <param name="resourceId">Identifier of the resource.</param>
    /// <returns>The command.</returns>
    public UpdateResourceCommand ToCommand(ResourceId resourceId)
    {
        return new UpdateResourceCommand(
            resourceId,
            Title,
            Subtitle,
            Description,
            Url,
            ResourceTypeId,
            StoredFileId.From(ThumbnailId)
        );
    }
}
