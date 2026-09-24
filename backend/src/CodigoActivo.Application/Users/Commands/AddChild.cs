using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to add a child.
/// </summary>
/// <param name="ParentId">Identifier of the parent.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record AddChildCommand(Guid ParentId, RegisterMinorRequest Request)
    : ICommand<Result<UserResponse>>;

/// <summary>
/// Executes the command to add a child.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
/// <param name="getById">Handler used to retrieve user by identifier.</param>
public sealed class AddChildCommandHandler(
    IUserRepository users,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator,
    GetUserByIdQueryHandler getById
) : ICommandHandler<AddChildCommand, Result<UserResponse>>
{
    /// <summary>
    /// Handles the request to add a child.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a user on success, or an application error on failure.</returns>
    public async Task<Result<UserResponse>> HandleAsync(
        AddChildCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var parent = await users.FindAsync(u => u.Id == command.ParentId, ct);
        if (parent is null)
        {
            return Error.NotFound(ErrorCode.ParentUserNotFound);
        }

        if (parent.ParentId is not null)
        {
            return Error.BadRequest(ErrorCode.UserParentIsMinor);
        }

        if (!request.BirthDate.IsMinor(clock.Today))
        {
            return Error.BadRequest(ErrorCode.UserChildBirthDateNotMinor);
        }

        var now = clock.UtcNow;
        var child = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            BirthDate = request.BirthDate,
            Gender = request.Gender,
            ParentId = command.ParentId,
            UserStatusTypeId = SeedIds.UserStatusTypes.Dependent,
            UserTypeId = SeedIds.UserTypes.Participant,
            CreatedAt = now,
        };
        await users.AddAsync(child, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Users);

        return await getById.HandleAsync(new GetUserByIdQuery(child.Id), ct);
    }
}
