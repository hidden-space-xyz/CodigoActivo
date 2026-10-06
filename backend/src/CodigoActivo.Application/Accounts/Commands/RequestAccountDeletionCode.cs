using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to email the code that confirms deleting an account.
/// </summary>
/// <param name="CurrentPassword">Password of the signed-in user, re-entered to authorize the change.</param>
public sealed record RequestAccountDeletionCodeCommand(
    [property: Required, MaxLength(128), NotBlank] string CurrentPassword
) : ICommand<Result>;

/// <summary>
/// Executes the command that emails the one-time code confirming an account deletion. The
/// password is demanded first so a stolen session cannot start the flow, and wrong passwords count
/// towards the same account lock as logins. The code reuses the storage, lifetime, cooldown and
/// lockout of the emailed login code. Users whose second factor is an authenticator application
/// read their code from it and never reach this command, and the initial administrator, which can
/// never be deleted, is refused before its password is checked.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="options">Second-factor configuration.</param>
/// <param name="loginCodes">Issuer of emailed one-time codes.</param>
public sealed class RequestAccountDeletionCodeCommandHandler(
    IUserRepository users,
    ICurrentUser currentUser,
    IClock clock,
    PasswordAttemptGuard passwordAttempts,
    TwoFactorOptions options,
    LoginCodeIssuer loginCodes
) : ICommandHandler<RequestAccountDeletionCodeCommand, Result>
{
    /// <summary>
    /// Handles the request to email the account deletion code.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        RequestAccountDeletionCodeCommand command,
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

        if (user.TwoFactorMethod != TwoFactorMethod.Email)
        {
            return Error.Conflict(ApplicationErrorCode.TwoFactorResendNotAllowed);
        }

        var now = clock.UtcNow;
        if (user.IsTwoFactorLocked(now))
        {
            return Error.Forbidden(ApplicationErrorCode.TwoFactorLocked);
        }

        if (user.IsLoginCodeResendCoolingDown(now, options.ResendCooldown))
        {
            return Error.Conflict(ApplicationErrorCode.TwoFactorResendCooldownActive);
        }

        var issued = await loginCodes.IssueAccountDeletionAsync(user, now, ct);
        if (issued.IsFailure)
        {
            return issued;
        }

        return Result.Success();
    }
}
