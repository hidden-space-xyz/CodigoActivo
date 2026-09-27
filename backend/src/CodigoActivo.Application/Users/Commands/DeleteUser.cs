using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to delete the user.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="ActingUserId">Identifier of the user that asked for the deletion.</param>
public sealed record DeleteUserCommand(Guid UserId, Guid ActingUserId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the user, keeping the blocked copy the law requires and handing
/// the content credited to the account over to the initial administrator. It serves an
/// administrator removing somebody else, another administrator included, and a guardian removing
/// one of their minors; deleting one's own account goes through
/// <see cref="DeleteOwnAccountCommand"/>, which also demands the password and the second factor.
/// The initial administrator is never deleted.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="deletedAccounts">Repository that erases the account after copying it.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class DeleteUserCommandHandler(
    IUserRepository users,
    IDeletedAccountRepository deletedAccounts,
    IClock clock,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeleteUserCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the user.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(DeleteUserCommand command, CancellationToken ct = default)
    {
        if (command.UserId == SeedIds.Users.InitialAdministrator)
        {
            return Error.Forbidden(ErrorCode.UserDeleteInitialAdminForbidden);
        }

        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (command.UserId == command.ActingUserId)
        {
            return Error.Forbidden(ErrorCode.UserSelfDeleteRequiresVerification);
        }

        var origin =
            user.ParentId == command.ActingUserId
                ? AccountDeletionOrigin.Guardian
                : AccountDeletionOrigin.Administrator;
        var erasure = new AccountErasure(origin, command.ActingUserId, clock.UtcNow);
        if (!await deletedAccounts.EraseAsync(user, erasure, ct))
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        await cacheInvalidator.InvalidateAsync(CacheTags.Erasure);
        return Result.Success();
    }
}
