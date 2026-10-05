using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Users;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to delete the signed-in user's own account.
/// </summary>
/// <param name="UserId">Identifier of the signed-in user.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record DeleteOwnAccountCommand(Guid UserId, DeleteAccountRequest Request)
    : ICommand<Result>;

/// <summary>
/// Executes the command that erases the signed-in user, every minor under their guardianship and
/// all their participation rows, keeping the blocked copy the law requires and handing the content
/// credited to the account over to the initial administrator. Because the deletion cannot be undone
/// it demands both the current password and the account's second factor, and wrong codes count
/// towards the same lockout as logins, checked with the account row locked. The initial
/// administrator cannot delete itself, so the application always keeps an administrator.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="accountEraser">Use case that erases the account after copying it.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="otpValidator">Validator of emailed codes.</param>
/// <param name="authenticatorCodes">Verifier of authenticator codes.</param>
/// <param name="options">Second-factor configuration.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class DeleteOwnAccountCommandHandler(
    IUserRepository users,
    AccountEraser accountEraser,
    IUnitOfWork uow,
    IClock clock,
    PasswordAttemptGuard passwordAttempts,
    OtpValidator otpValidator,
    AuthenticatorCodeVerifier authenticatorCodes,
    TwoFactorOptions options,
    ICacheInvalidator cacheInvalidator,
    ILogger<DeleteOwnAccountCommandHandler> logger
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
        var deletable = InitialAdministrator.EnsureMayBeDeleted(command.UserId);
        if (deletable.IsFailure)
        {
            return deletable.Error!;
        }

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (
            !await passwordAttempts.VerifyReauthenticationAsync(
                user,
                command.Request.CurrentPassword,
                ct
            )
        )
        {
            return Error.Validation(ErrorCode.UserCurrentPasswordIncorrect);
        }

        var verified = await uow.ExecuteInTransactionAsync(
            attempt => VerifyCodeLockedAsync(user, command.Request.Code, attempt),
            ct
        );
        if (verified.IsFailure)
        {
            return verified;
        }

        var erasure = AccountErasure.For(user, user.Id, clock.UtcNow);
        if (!await accountEraser.EraseAsync(user, erasure, ct))
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        await cacheInvalidator.InvalidateAsync(CacheTags.Erasure);
        return Result.Success();
    }

    private async Task<Result> VerifyCodeLockedAsync(User user, string code, CancellationToken ct)
    {
        if (!await users.LockAsync(user, ct))
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        var now = clock.UtcNow;
        if (user.IsTwoFactorLocked(now))
        {
            return Error.Forbidden(ErrorCode.TwoFactorLocked);
        }

        if (IsCodeAccepted(user, code, now))
        {
            return Result.Success();
        }

        var locked = user.RecordTwoFactorFailure(
            now,
            options.MaxFailedAttempts,
            options.LockoutDuration
        );
        await uow.SaveChangesAsync(ct);
        if (locked)
        {
            logger.TwoFactorLockoutTriggered(options.MaxFailedAttempts);
        }

        return Error.Validation(ErrorCode.TwoFactorCodeInvalid);
    }

    private bool IsCodeAccepted(User user, string code, DateTimeOffset now)
    {
        return user.TwoFactorMethod switch
        {
            TwoFactorMethod.Authenticator => authenticatorCodes.Match(user.AuthenticatorKey, code)
                is { } step
                && user.AcceptsAuthenticatorStep(step),
            _ => otpValidator.IsCodeValid(code, user.UsableLoginCodeHash(now)),
        };
    }
}
