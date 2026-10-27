using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Erasure;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to delete the user.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
public sealed record DeleteUserCommand(UserId UserId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the user and their dependents, keeping their data history blocked
/// and handing the content credited to them over to the initial administrator. It serves an
/// administrator removing somebody else, another administrator included, and a guardian removing
/// one of their minors; deleting one's own account goes through
/// <see cref="DeleteOwnAccountCommand"/>, which also demands the password and the second factor.
/// The initial administrator is never deleted.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="actingUser">Policy that decides for whom the signed-in user may act.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="accountEraser">Eraser that deletes the account and its dependents, keeping their data history.</param>
public sealed class DeleteUserCommandHandler(
    IUserRepository users,
    ActingUserPolicy actingUser,
    ICurrentUser currentUser,
    AccountEraser accountEraser
) : ICommandHandler<DeleteUserCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the user.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success;
    /// <see cref="ApplicationErrorCode.ActingForAnotherUserForbidden"/> when the caller is neither an
    /// administrator nor the guardian, <see cref="DomainErrorCode.UserDeleteInitialAdminForbidden"/>
    /// for the initial administrator,
    /// <see cref="ApplicationErrorCode.UserSelfDeleteRequiresVerification"/> for the caller's own
    /// account, or <see cref="ApplicationErrorCode.UserNotFound"/> when the user is missing or was
    /// erased meanwhile.
    /// </returns>
    public async Task<Result> HandleAsync(DeleteUserCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var allowed = await actingUser.EnsureMayActForAsync(command.UserId, ct);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        var deletable = InitialAdministrator.EnsureMayBeDeleted(command.UserId);
        if (deletable.IsFailure)
        {
            return deletable.Error!;
        }

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        var actingUserId = currentUser.RequiredId();
        if (command.UserId == actingUserId)
        {
            return Error.Forbidden(ApplicationErrorCode.UserSelfDeleteRequiresVerification);
        }

        return await accountEraser.EraseAsync(user, ct)
            ? Result.Success()
            : Error.NotFound(ApplicationErrorCode.UserNotFound);
    }
}
