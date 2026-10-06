using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Resources;

namespace CodigoActivo.Application.Resources.Commands;

/// <summary>
/// Carries the input required to delete the resource.
/// </summary>
/// <param name="ResourceId">Identifier of the resource.</param>
public sealed record DeleteResourceCommand(ResourceId ResourceId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the resource.
/// </summary>
/// <param name="resources">Repository used to persist and retrieve resources.</param>
public sealed class DeleteResourceCommandHandler(IResourceRepository resources)
    : ICommandHandler<DeleteResourceCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the resource.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        DeleteResourceCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var resource = await resources.GetByIdAsync(command.ResourceId, ct);
        if (resource is null)
        {
            return Error.NotFound(ApplicationErrorCode.ResourceNotFound);
        }

        resource.Delete();
        resources.Remove(resource);
        return Result.Success();
    }
}
