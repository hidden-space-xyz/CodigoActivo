using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to email the code that confirms a change of the signed-in user's
/// own account.
/// </summary>
/// <param name="CurrentPassword">
/// Password of the signed-in user, re-entered to authorize the change.
/// </param>
/// <param name="Purpose">Change the code is asked for, which the email names.</param>
public sealed record RequestConfirmationCodeCommand(
    [property: Required, MaxLength(128)] string CurrentPassword,
    [property: EnumDataType(typeof(ConfirmationCodePurpose))] ConfirmationCodePurpose Purpose
) : ICommand<Result>, IPreparatoryCommand;

/// <summary>
/// Executes the command that emails the one-time code confirming a change that a session and the
/// password alone may not make: deleting the account, moving it to another email or enrolling an
/// authenticator application. The password is demanded first so a stolen session cannot start the
/// flow, and wrong passwords count towards the same account lock as logins. The code reuses the
/// storage, lifetime, cooldown and lockout of the emailed login code, so it reaches the current
/// address and is accepted by any of those changes. Users whose second factor is an authenticator
/// application read their code from it and are refused here, and the initial administrator, which
/// can never be deleted, is refused a deletion code before its password is checked. The code is
/// issued with the account locked, so parallel requests run one after another and the later ones
/// find the resend cooldown.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="accountLock">Runs the issue of the code with the account locked.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="loginCodes">Issuer of emailed one-time codes.</param>
public sealed class RequestConfirmationCodeCommandHandler(
    IUserRepository users,
    AccountLock accountLock,
    ICurrentUser currentUser,
    IClock clock,
    PasswordAttemptGuard passwordAttempts,
    LoginCodeIssuer loginCodes
) : ICommandHandler<RequestConfirmationCodeCommand, Result>
{
    /// <summary>
    /// Handles the request to email the confirmation code.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result indicates success,
    /// <see cref="DomainErrorCode.UserDeleteInitialAdminForbidden"/> when the initial
    /// administrator asks for a deletion code,
    /// <see cref="ApplicationErrorCode.UserCurrentPasswordIncorrect"/>, or the refusal of
    /// <see cref="LoginCodeIssuer"/>, such as
    /// <see cref="DomainErrorCode.TwoFactorResendNotAllowed"/> for an authenticator application
    /// or <see cref="ApplicationErrorCode.TwoFactorResendCooldownActive"/> within the resend
    /// cooldown.
    /// </returns>
    public async Task<Result> HandleAsync(
        RequestConfirmationCodeCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Purpose is ConfirmationCodePurpose.AccountDeletion)
        {
            var deletable = InitialAdministrator.EnsureMayBeDeleted(currentUser.RequiredId());
            if (deletable.IsFailure)
            {
                return deletable.Error!;
            }
        }

        var user = await users.GetByIdAsync(currentUser.RequiredId(), ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        if (!await passwordAttempts.VerifyReauthenticationAsync(user, command.CurrentPassword, ct))
        {
            return CurrentPasswordIncorrect();
        }

        var verifiedHash = user.PasswordHash;
        return loginCodes.RefusalOf(user, clock.UtcNow)
            ?? await accountLock.RunAsync(
                user,
                attempt => IssueLockedAsync(user, command.Purpose, verifiedHash, attempt),
                Error.NotFound(ApplicationErrorCode.UserNotFound),
                ct
            );
    }

    private static Result CurrentPasswordIncorrect()
    {
        return Error.Validation(ApplicationErrorCode.UserCurrentPasswordIncorrect);
    }

    private async Task<Result> IssueLockedAsync(
        User user,
        ConfirmationCodePurpose purpose,
        string? verifiedHash,
        CancellationToken ct
    )
    {
        if (!PasswordAttemptGuard.StillVerified(user, verifiedHash, clock.UtcNow))
        {
            return CurrentPasswordIncorrect();
        }

        return loginCodes.RefusalOf(user, clock.UtcNow)
            ?? await loginCodes.IssueConfirmationAsync(user, purpose, clock.UtcNow, ct);
    }
}
