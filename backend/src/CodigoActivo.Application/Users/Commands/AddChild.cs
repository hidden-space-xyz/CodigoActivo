using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to add a minor to a household.
/// </summary>
/// <param name="ParentId">Identifier of the guardian.</param>
/// <param name="Child">Details of the minor.</param>
public sealed record AddChildCommand(UserId ParentId, MinorDraft Child) : ICommand<Result<UserId>>;

/// <summary>
/// Executes the command to add a minor to a household. The signed-in user may add one to their
/// own household; an administrator to anyone's.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="actingUser">Policy that decides for whom the signed-in user may act.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class AddChildCommandHandler(
    IUserRepository users,
    ActingUserPolicy actingUser,
    IClock clock,
    IUnitOfWork uow
) : ICommandHandler<AddChildCommand, Result<UserId>>
{
    /// <summary>
    /// Handles the request to add a minor to a household.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifier of the minor, or an application error on failure.</returns>
    public async Task<Result<UserId>> HandleAsync(
        AddChildCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var allowed = await actingUser.EnsureMayActForAsync(command.ParentId, ct);
        if (allowed.IsFailure)
        {
            return allowed.Error!;
        }

        var parent = await users.GetByIdAsync(command.ParentId, ct);
        if (parent is null)
        {
            return Error.NotFound(ApplicationErrorCode.ParentUserNotFound);
        }

        var child = User.CreateDependent(
            parent,
            command.Child.ToDetails(),
            clock.Today,
            clock.UtcNow
        );
        if (child.IsFailure)
        {
            return child.Error!;
        }

        var added = await uow.ExecuteInTransactionAsync(
            attempt => AddLockedAsync(parent, child.Value, attempt),
            ct
        );
        if (added.IsFailure)
        {
            return added.Error!;
        }

        return child.Value.Id;
    }

    private async Task<Result> AddLockedAsync(User parent, User child, CancellationToken ct)
    {
        if (!await users.LockAsync(parent, ct))
        {
            return Error.NotFound(ApplicationErrorCode.ParentUserNotFound);
        }

        var allowed = Household.EnsureMayAddDependent(
            await users.CountDependentsAsync(parent.Id, ct)
        );
        if (allowed.IsFailure)
        {
            return allowed;
        }

        await users.AddAsync(child, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
