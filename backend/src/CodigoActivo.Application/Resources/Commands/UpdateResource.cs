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
/// Carries the input required to update the resource.
/// </summary>
/// <param name="ResourceId">Identifier of the resource.</param>
/// <param name="Title">Title, up to 200 characters.</param>
/// <param name="Subtitle">Line shown below the title, up to 300 characters.</param>
/// <param name="Description">
/// Body as rich text JSON; required for an internal resource and refused for an external one.
/// </param>
/// <param name="Url">
/// HTTP or HTTPS link of up to 500 characters; required for an external resource and refused for
/// an internal one.
/// </param>
/// <param name="Type">Whether the resource is written on the site or links elsewhere.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file, which must exist.</param>
public sealed record UpdateResourceCommand(
    ResourceId ResourceId,
    [property: Required, MaxLength(200), NotBlank] string Title,
    [property: Required, MaxLength(300), NotBlank] string Subtitle,
    [property: RichText, MaxLength(RichTextAllowlist.MaxJsonLength)] string? Description,
    [property: HttpUrl, MaxLength(500)] string? Url,
    ResourceType Type,
    StoredFileId ThumbnailId
) : ICommand<Result>;

/// <summary>
/// Executes the command to update the resource. A thumbnail or body image it no longer references
/// is removed after the commit when nothing else uses it.
/// </summary>
/// <param name="resources">Repository used to persist and retrieve resources.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="currentUser">Person the use case runs for, recorded as the editor.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work that saves the resource in the transaction that locks its files.</param>
public sealed class UpdateResourceCommandHandler(
    IResourceRepository resources,
    IStoredFileRepository files,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork uow
) : ICommandHandler<UpdateResourceCommand, Result>
{
    /// <summary>
    /// Handles the request to update the resource.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success; <see cref="ApplicationErrorCode.ResourceNotFound"/>;
    /// the domain error of a body or link the type does not allow or lacks, or
    /// <see cref="ApplicationErrorCode.ResourceThumbnailNotFound"/> or
    /// <see cref="ApplicationErrorCode.RichTextFileNotFound"/> when a file is missing.
    /// </returns>
    public async Task<Result> HandleAsync(
        UpdateResourceCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var resource = await resources.GetByIdAsync(command.ResourceId, ct);
        if (resource is null)
        {
            return Error.NotFound(ApplicationErrorCode.ResourceNotFound);
        }

        var description = RichText.FromOptional(command.Description);
        var content = ResourceContent.For(command.Type, description, command.Url);
        if (content.IsFailure)
        {
            return content.Error!;
        }

        return await uow.ExecuteInTransactionAsync(
            attempt => UpdateLockedAsync(resource, command, description, content.Value, attempt),
            ct
        );
    }

    private async Task<Result> UpdateLockedAsync(
        Resource resource,
        UpdateResourceCommand command,
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
            return available;
        }

        resource.Update(
            new ResourceDetails(command.Title, command.Subtitle, command.ThumbnailId),
            content,
            currentUser.RequiredId(),
            clock.UtcNow
        );
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
