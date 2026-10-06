using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Resources;

namespace CodigoActivo.Application.Resources.Commands;

/// <summary>
/// Carries the input required to create a resource.
/// </summary>
/// <param name="Title">Title.</param>
/// <param name="Subtitle">Line shown below the title.</param>
/// <param name="Description">Body as rich text JSON, for resources that carry one.</param>
/// <param name="Url">Link, for resources that point elsewhere.</param>
/// <param name="ResourceTypeId">Catalog identifier of the resource type.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
public sealed record CreateResourceCommand(
    [property: Required, MaxLength(200), NotBlank] string Title,
    [property: Required, MaxLength(300), NotBlank] string Subtitle,
    [property: RichText, MaxLength(262144)] string? Description,
    [property: HttpUrl, MaxLength(500)] string? Url,
    Guid ResourceTypeId,
    StoredFileId ThumbnailId
) : ICommand<Result<ResourceId>>;

/// <summary>
/// Executes the command to create a resource.
/// </summary>
/// <param name="resources">Repository used to persist and retrieve resources.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class CreateResourceCommandHandler(
    IResourceRepository resources,
    IStoredFileRepository files,
    ICurrentUser currentUser,
    IClock clock
) : ICommandHandler<CreateResourceCommand, Result<ResourceId>>
{
    /// <summary>
    /// Handles the request to create a resource.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifier of the created item, or an application error on failure.</returns>
    public async Task<Result<ResourceId>> HandleAsync(
        CreateResourceCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!CatalogIds.ResourceTypes.TryGetValue(command.ResourceTypeId, out var type))
        {
            return Error.Validation(ApplicationErrorCode.ResourceTypeNotFound);
        }

        var content = ResourceContent.For(
            type,
            RichText.FromOptional(command.Description),
            command.Url
        );
        if (content.IsFailure)
        {
            return content.Error!;
        }

        if (!await files.ExistsAsync(command.ThumbnailId, ct))
        {
            return Error.Validation(ApplicationErrorCode.ResourceThumbnailNotFound);
        }

        var resource = Resource.Create(
            new ResourceDetails(command.Title, command.Subtitle, type, command.ThumbnailId),
            content.Value,
            currentUser.RequiredId(),
            clock.UtcNow
        );
        await resources.AddAsync(resource, ct);
        return resource.Id;
    }
}
