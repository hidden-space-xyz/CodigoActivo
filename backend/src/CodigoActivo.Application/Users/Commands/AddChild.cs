using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to add a minor to a household.
/// </summary>
/// <param name="ParentId">Identifier of the guardian.</param>
/// <param name="Child">Details of the minor.</param>
public sealed record AddChildCommand(UserId ParentId, MinorDraft Child)
    : ICommand<Result<UserId>>,
        IRoutineCommand;

/// <summary>
/// Executes the command to add a minor to a household. Only the guardian may add one to their own
/// household; an administrator cannot add minors to anyone else's.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="actingUser">
/// Policy that decides whose personal data the signed-in user manages.
/// </param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class AddChildCommandHandler(
    IUserRepository users,
    ActingUserPolicy actingUser,
    IClock clock
) : ICommandHandler<AddChildCommand, Result<UserId>>
{
    /// <summary>
    /// Handles the request to add a minor to a household.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the identifier of the minor;
    /// <see cref="ApplicationErrorCode.ActingForAnotherUserForbidden"/> when the caller is not the
    /// guardian, <see cref="ApplicationErrorCode.ParentUserNotFound"/> when the guardian is
    /// missing, <see cref="DomainErrorCode.UserChildLimitReached"/> once the household is full, or
    /// the domain error of the guardianship or birth date rule that refuses the minor.
    /// </returns>
    public async Task<Result<UserId>> HandleAsync(
        AddChildCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var allowed = await actingUser.EnsureMayManagePersonalDataAsync(command.ParentId, ct);
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
            command.Child.DeclaresGuardianship,
            clock.Today,
            clock.UtcNow
        );
        if (child.IsFailure)
        {
            return child.Error!;
        }

        var room = Household.EnsureMayAddDependent(await users.CountDependentsAsync(parent.Id, ct));
        if (room.IsFailure)
        {
            return room.Error!;
        }

        await users.AddAsync(child.Value, ct);
        return child.Value.Id;
    }
}
