using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to delete the signed-in user's own account.
/// </summary>
/// <param name="UserId">Identifier of the signed-in user.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record DeleteOwnAccountCommand(Guid UserId, DeleteAccountRequest Request)
    : ICommand<Result>;

/// <summary>
/// Executes the command that erases the signed-in user, every minor under their guardianship and
/// all their participation rows. Because the deletion cannot be undone it demands both the current
/// password and the account's second factor, and wrong codes count towards the same lockout as
/// logins. Administrators cannot delete themselves, and an account still credited as the author of
/// published content is refused instead of breaking those rows.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="hasher">Hasher used to verify the current password.</param>
/// <param name="otpValidator">Validator of emailed codes.</param>
/// <param name="authenticatorCodes">Verifier of authenticator codes.</param>
/// <param name="options">Second-factor configuration.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class DeleteOwnAccountCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    OtpValidator otpValidator,
    AuthenticatorCodeVerifier authenticatorCodes,
    TwoFactorOptions options,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeleteOwnAccountCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the signed-in user's own account.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        DeleteOwnAccountCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (user.IsAdmin)
        {
            return Error.Forbidden(ErrorCode.UserDeleteAdminForbidden);
        }

        if (
            string.IsNullOrEmpty(user.PasswordHash)
            || !hasher.Verify(command.Request.CurrentPassword, user.PasswordHash)
        )
        {
            return Error.BadRequest(ErrorCode.UserCurrentPasswordIncorrect);
        }

        var now = clock.UtcNow;
        if (user.IsTwoFactorLocked(now))
        {
            return Error.Forbidden(ErrorCode.TwoFactorLocked);
        }

        if (!IsCodeAccepted(user, command.Request.Code))
        {
            user.RecordTwoFactorFailure(now, options.MaxFailedAttempts, options.LockoutDuration);
            await uow.SaveChangesAsync(ct);
            return Error.BadRequest(ErrorCode.TwoFactorCodeInvalid);
        }

        if (await users.HasAuthoredContentAsync(user.Id, ct))
        {
            return Error.Conflict(ErrorCode.UserDeleteAuthoredContentExists);
        }

        users.Remove(user);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Users, CacheTags.Activities);
        return Result.Success();
    }

    private bool IsCodeAccepted(User user, string code)
    {
        return user.TwoFactorMethod switch
        {
            TwoFactorMethod.Authenticator => authenticatorCodes.Match(
                user.AuthenticatorKey,
                code,
                user.AuthenticatorLastUsedStep
            )
                is not null,
            _ => otpValidator.IsCodeValid(code, user.LoginCodeHash, user.LoginCodeExpiresAt),
        };
    }
}
