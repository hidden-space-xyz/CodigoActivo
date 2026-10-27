using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.BlockedData.Commands;

/// <summary>
/// Carries the input required to email the code that confirms opening a consultation of the
/// blocked data.
/// </summary>
/// <param name="CurrentPassword">Password of the signed-in user, re-entered to authorize it.</param>
public sealed record RequestBlockedDataConsultationCodeCommand(
    [property: Required, MaxLength(128), NotBlank] string CurrentPassword
) : ICommand<Result>;

/// <summary>
/// Executes the command that emails the one-time code confirming the opening of a consultation of
/// the blocked data. Only the account designated for data protection asks for it, and its password
/// is demanded first so a stolen session cannot start the flow; wrong passwords count towards the
/// same account lock as logins. The code reuses the storage, lifetime, cooldown and lockout of the
/// emailed login code. Accounts whose second factor is an authenticator application read their
/// code from it and never reach this command. The code is issued with the account locked, so
/// parallel requests run one after another and the later ones find the resend cooldown.
/// </summary>
/// <param name="officer">Policy that tells who may consult the blocked data.</param>
/// <param name="accountLock">Runs the issue of the code with the account locked.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="loginCodes">Issuer of emailed one-time codes.</param>
public sealed class RequestBlockedDataConsultationCodeCommandHandler(
    PrivacyOfficer officer,
    AccountLock accountLock,
    IClock clock,
    PasswordAttemptGuard passwordAttempts,
    LoginCodeIssuer loginCodes
) : ICommandHandler<RequestBlockedDataConsultationCodeCommand, Result>
{
    /// <summary>
    /// Handles the request to email the code that confirms opening a consultation.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result indicates success,
    /// <see cref="ApplicationErrorCode.BlockedDataOfficerOnly"/> for any other account,
    /// <see cref="ApplicationErrorCode.UserCurrentPasswordIncorrect"/>, or the refusal of
    /// <see cref="LoginCodeIssuer"/>, such as
    /// <see cref="ApplicationErrorCode.TwoFactorResendCooldownActive"/> within the resend cooldown.
    /// </returns>
    public async Task<Result> HandleAsync(
        RequestBlockedDataConsultationCodeCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var signedIn = await officer.SignedInOfficerAsync(ct);
        if (signedIn.IsFailure)
        {
            return signedIn.Error!;
        }

        var user = signedIn.Value;
        if (!await passwordAttempts.VerifyReauthenticationAsync(user, command.CurrentPassword, ct))
        {
            return CurrentPasswordIncorrect();
        }

        var verifiedHash = user.PasswordHash;
        return loginCodes.RefusalOf(user, clock.UtcNow)
            ?? await accountLock.RunAsync(
                user,
                attempt => IssueLockedAsync(user, verifiedHash, attempt),
                Error.Forbidden(ApplicationErrorCode.BlockedDataOfficerOnly),
                ct
            );
    }

    private static Result CurrentPasswordIncorrect()
    {
        return Error.Validation(ApplicationErrorCode.UserCurrentPasswordIncorrect);
    }

    private async Task<Result> IssueLockedAsync(
        User user,
        string? verifiedHash,
        CancellationToken ct
    )
    {
        if (!PasswordAttemptGuard.StillVerified(user, verifiedHash))
        {
            return CurrentPasswordIncorrect();
        }

        return loginCodes.RefusalOf(user, clock.UtcNow)
            ?? await loginCodes.IssueBlockedDataConsultationAsync(user, clock.UtcNow, ct);
    }
}
