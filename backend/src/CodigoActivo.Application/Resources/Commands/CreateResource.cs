using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Resources.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Resources;

namespace CodigoActivo.Application.Resources.Commands;

/// <summary>
/// Carries the input required to create a resource.
/// </summary>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record CreateResourceCommand(CreateResourceRequest Request, Guid UserId)
    : ICommand<Result<Guid>>;

/// <summary>
/// Executes the command to create a resource.
/// </summary>
/// <param name="resources">Repository used to persist and retrieve resources.</param>
/// <param name="readStore">Read side used to check the resource type catalog.</param>
/// <param name="executor">Executor of the read-side queries.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class CreateResourceCommandHandler(
    IResourceRepository resources,
    IReadStore readStore,
    IQueryExecutor executor,
    IFileRepository files,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<CreateResourceCommand, Result<Guid>>
{
    /// <summary>
    /// Handles the request to create a resource.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifier of the created item, or an application error on failure.</returns>
    public async Task<Result<Guid>> HandleAsync(
        CreateResourceCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var isExternal = await executor.FirstOrDefaultAsync(
            readStore
                .ResourceTypes.Where(type => type.Id == request.ResourceTypeId)
                .Select(type => (bool?)type.IsExternal),
            ct
        );
        if (isExternal is null)
        {
            return Error.Validation(ErrorCode.ResourceTypeNotFound);
        }

        var content = ResourceContent.For(isExternal.Value, request.Description, request.Url);
        if (content.IsFailure)
        {
            return content.Error!;
        }

        if (!await files.ExistsAsync(request.ThumbnailId, ct))
        {
            return Error.Validation(ErrorCode.ResourceThumbnailNotFound);
        }

        var resource = Resource.Create(
            new ResourceDetails(
                request.Title,
                request.Subtitle,
                request.ResourceTypeId,
                request.ThumbnailId
            ),
            content.Value,
            command.UserId,
            clock.UtcNow
        );
        await resources.AddAsync(resource, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Resources);
        return resource.Id;
    }
}
