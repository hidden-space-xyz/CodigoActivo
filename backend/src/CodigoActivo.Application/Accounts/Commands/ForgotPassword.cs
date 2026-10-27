using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to ask for a password reset link.
/// </summary>
/// <param name="Email">Email of the account.</param>
public sealed record ForgotPasswordCommand(
    [property: Required, EmailAddress, MaxLength(256)] string Email
) : ICommand<Result>;

/// <summary>
/// Executes the command that emails a password reset link, replacing the previous one, to an
/// account that has a password and is neither blocked nor a dependent.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="accountLock">Runs the issue of the code with the account locked.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="codeHasher">Hasher used so the reset code is never stored in plaintext.</param>
/// <param name="passwordReset">Password reset configuration: code lifetime and resend cooldown.</param>
/// <param name="accountEmails">Sender of the password reset link.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
/// <param name="auditSubject">Names the account the request found for the audit log.</param>
public sealed class ForgotPasswordCommandHandler(
    IUserRepository users,
    AccountLock accountLock,
    IClock clock,
    IOneTimeCodeHasher codeHasher,
    PasswordResetOptions passwordReset,
    AccountEmails accountEmails,
    ILogger<ForgotPasswordCommandHandler> logger,
    IAuditSubject auditSubject
) : ICommandHandler<ForgotPasswordCommand, Result>
{
    /// <summary>
    /// Handles the request for a password reset link. Every request succeeds alike, so the answer
    /// never tells whether the address has an account. Only an account that will get a code is
    /// locked, so an address that gets nothing costs the same as one without an account; the code
    /// is issued with the account locked and its eligibility checked again, so parallel requests
    /// run one after another and the later ones find the resend cooldown. A mail the limiter
    /// refuses or that cannot be queued stores no code.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result always reports success.</returns>
    public async Task<Result> HandleAsync(
        ForgotPasswordCommand command,
        CancellationToken ct = default
    )
    {
        var email = EmailAddress.Create(command.Email);
        var user = email.IsSuccess ? await users.GetByEmailAsync(email.Value, ct) : null;
        if (user is not null)
        {
            auditSubject.Name(user.Id);
        }

        return user is not null && MayIssue(user)
            ? await accountLock.RunAsync(
                user,
                attempt => IssueLockedAsync(user, attempt),
                Result.Success(),
                ct
            )
            : Result.Success();
    }

    private bool MayIssue(User user)
    {
        return !string.IsNullOrEmpty(user.PasswordHash)
            && !user.IsBlocked
            && !user.IsDependent
            && !user.PasswordResetCode.IsResendCoolingDown(
                clock.UtcNow,
                passwordReset.ResendCooldown
            );
    }

    private async Task<Result> IssueLockedAsync(User user, CancellationToken ct)
    {
        if (!MayIssue(user))
        {
            return Result.Success();
        }

        var now = clock.UtcNow;

        var code = AccountTokens.Create();
        try
        {
            await accountEmails.SendPasswordResetEmailAsync(user, code, ct);
        }
        catch (EmailRateLimitedException)
        {
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailSendFailed(EmailKind.AccessRecovery, ex);
            return Result.Success();
        }

        user.IssuePasswordResetCode(codeHasher.Hash(code), now, passwordReset.CodeLifetime);
        return Result.Success();
    }
}
