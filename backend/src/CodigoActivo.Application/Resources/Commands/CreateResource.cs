using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Application.Files;
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
/// <param name="Type">Kind of resource.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
public sealed record CreateResourceCommand(
    [property: Required, MaxLength(200), NotBlank] string Title,
    [property: Required, MaxLength(300), NotBlank] string Subtitle,
    [property: RichText, MaxLength(RichTextAllowlist.MaxJsonLength)] string? Description,
    [property: HttpUrl, MaxLength(500)] string? Url,
    ResourceType Type,
    StoredFileId ThumbnailId
) : ICommand<Result<ResourceId>>;

/// <summary>
/// Executes the command to create a resource.
/// </summary>
/// <param name="resources">Repository used to persist and retrieve resources.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work that saves the resource in the transaction that locks its files.</param>
public sealed class CreateResourceCommandHandler(
    IResourceRepository resources,
    IStoredFileRepository files,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork uow
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

        var description = RichText.FromOptional(command.Description);
        var content = ResourceContent.For(command.Type, description, command.Url);
        if (content.IsFailure)
        {
            return content.Error!;
        }

        return await uow.ExecuteInTransactionAsync(
            attempt => CreateLockedAsync(command, description, content.Value, attempt),
            ct
        );
    }

    private async Task<Result<ResourceId>> CreateLockedAsync(
        CreateResourceCommand command,
        RichText? description,
        ResourceContent content,
        CancellationToken ct
    )
    {
        var available = await ContentFiles.EnsureAvailableAsync(
            files,
            command.ThumbnailId,
            description,
            ApplicationErrorCode.ResourceThumbnailNotFound,
            ct
        );
        if (available.IsFailure)
        {
            return available.Error!;
        }

        var resource = Resource.Create(
            new ResourceDetails(command.Title, command.Subtitle, command.ThumbnailId),
            content,
            currentUser.RequiredId(),
            clock.UtcNow
        );
        await resources.AddAsync(resource, ct);
        await uow.SaveChangesAsync(ct);
        return resource.Id;
    }
}
