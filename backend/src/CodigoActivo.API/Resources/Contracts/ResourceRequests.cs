using System.ComponentModel.DataAnnotations;
using CodigoActivo.API.Contracts;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Application.Resources.Commands;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Resources;

namespace CodigoActivo.API.Resources.Contracts;

/// <summary>
/// Contains the client-supplied data used to create a resource.
/// </summary>
/// <param name="Title">Title of the resource.</param>
/// <param name="Subtitle">Line shown below the title.</param>
/// <param name="Description">
/// Body as rich text JSON; required for a resource written on the site and refused for an
/// external one.
/// </param>
/// <param name="Url">
/// HTTP or HTTPS link; required for an external resource and refused for one written on the site.
/// </param>
/// <param name="ResourceTypeId">Identifier of the resource type, which decides which of the two applies.</param>
/// <param name="ThumbnailId">Identifier of the uploaded thumbnail file, which must exist.</param>
public record CreateResourceRequest(
    [Required] [MaxLength(200)] string Title,
    [Required] [MaxLength(300)] string Subtitle,
    [MaxLength(RichTextAllowlist.MaxJsonLength)] string? Description,
    [MaxLength(500)] string? Url,
    Guid ResourceTypeId,
    Guid ThumbnailId
) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return CatalogIdentifiers.Check(
            CatalogIds.ResourceTypes,
            ResourceTypeId,
            nameof(ResourceTypeId)
        );
    }

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
            CatalogIds.ResourceTypes.ValueOf(ResourceTypeId),
            StoredFileId.From(ThumbnailId)
        );
    }
}

/// <summary>
/// Contains the client-supplied data that replaces every editable field of a resource.
/// </summary>
/// <param name="Title">Title of the resource.</param>
/// <param name="Subtitle">Line shown below the title.</param>
/// <param name="Description">
/// Body as rich text JSON; required for a resource written on the site and refused for an
/// external one.
/// </param>
/// <param name="Url">
/// HTTP or HTTPS link; required for an external resource and refused for one written on the site.
/// </param>
/// <param name="ResourceTypeId">Identifier of the resource type, which decides which of the two applies.</param>
/// <param name="ThumbnailId">Identifier of the uploaded thumbnail file, which must exist.</param>
public record UpdateResourceRequest(
    [Required] [MaxLength(200)] string Title,
    [Required] [MaxLength(300)] string Subtitle,
    [MaxLength(RichTextAllowlist.MaxJsonLength)] string? Description,
    [MaxLength(500)] string? Url,
    Guid ResourceTypeId,
    Guid ThumbnailId
) : IValidatableObject
{
    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return CatalogIdentifiers.Check(
            CatalogIds.ResourceTypes,
            ResourceTypeId,
            nameof(ResourceTypeId)
        );
    }

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
            CatalogIds.ResourceTypes.ValueOf(ResourceTypeId),
            StoredFileId.From(ThumbnailId)
        );
    }
}
