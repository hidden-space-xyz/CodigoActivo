using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Application.Users;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to delete the signed-in user's own account.
/// </summary>
/// <param name="CurrentPassword">Password of the signed-in user, re-entered to authorize the change.</param>
/// <param name="Code">Code of the second factor.</param>
public sealed record DeleteOwnAccountCommand(
    [property: Required, MaxLength(128), NotBlank] string CurrentPassword,
    [property: Required, MaxLength(16), NotBlank] string Code
) : ICommand<Result>;

/// <summary>
/// Executes the command that erases the signed-in user, every minor under their guardianship and
/// all their participation rows, keeping the blocked copy the law requires and handing the content
/// credited to the account over to the initial administrator. Because the deletion cannot be undone
/// it demands both the current password and the account's second factor, and wrong codes count
/// towards the same lockout as logins, checked with the account row locked. The initial
/// administrator cannot delete itself, so the application always keeps an administrator.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="accountEraser">Use case that erases the account after copying it.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="otpValidator">Validator of emailed codes.</param>
/// <param name="authenticatorCodes">Verifier of authenticator codes.</param>
/// <param name="options">Second-factor configuration.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class DeleteOwnAccountCommandHandler(
    IUserRepository users,
    ICurrentUser currentUser,
    AccountEraser accountEraser,
    IUnitOfWork uow,
    IClock clock,
    PasswordAttemptGuard passwordAttempts,
    OtpValidator otpValidator,
    AuthenticatorCodeVerifier authenticatorCodes,
    TwoFactorOptions options,
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
        var deletable = InitialAdministrator.EnsureMayBeDeleted(currentUser.RequiredId());
        if (deletable.IsFailure)
        {
            return deletable.Error!;
        }

        var user = await users.GetByIdAsync(currentUser.RequiredId(), ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        if (!await passwordAttempts.VerifyReauthenticationAsync(user, command.CurrentPassword, ct))
        {
            return Error.Validation(ApplicationErrorCode.UserCurrentPasswordIncorrect);
        }

        var verified = await uow.ExecuteInTransactionAsync(
            attempt => VerifyCodeLockedAsync(user, command.Code, attempt),
            ct
        );
        if (verified.IsFailure)
        {
            return verified;
        }

        var erasure = AccountErasure.For(user, user.Id, clock.UtcNow);
        if (!await accountEraser.EraseAsync(user, erasure, ct))
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }
        return Result.Success();
    }

    private async Task<Result> VerifyCodeLockedAsync(User user, string code, CancellationToken ct)
    {
        if (!await users.LockAsync(user, ct))
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        var now = clock.UtcNow;
        if (user.IsTwoFactorLocked(now))
        {
            return Error.Forbidden(ApplicationErrorCode.TwoFactorLocked);
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

        return Error.Validation(ApplicationErrorCode.TwoFactorCodeInvalid);
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
