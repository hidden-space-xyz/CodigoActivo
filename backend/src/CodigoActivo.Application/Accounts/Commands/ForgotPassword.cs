using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to forgot password.
/// </summary>
/// <param name="Email">Email of the account.</param>
public sealed record ForgotPasswordCommand(
    [property: Required, EmailAddress, MaxLength(256)] string Email
) : ICommand<Result>;

/// <summary>
/// Executes the command to forgot password.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="hasher">The hasher value.</param>
/// <param name="passwordReset">The password reset value.</param>
/// <param name="accountEmails">The account emails value.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ForgotPasswordCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    PasswordResetOptions passwordReset,
    AccountEmails accountEmails,
    ILogger<ForgotPasswordCommandHandler> logger
) : ICommandHandler<ForgotPasswordCommand, Result>
{
    /// <summary>
    /// Handles the request to forgot password. Every request succeeds alike and hashes a code
    /// first, so neither the answer nor its timing tells whether the address has an account.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result always reports success.</returns>
    public async Task<Result> HandleAsync(
        ForgotPasswordCommand command,
        CancellationToken ct = default
    )
    {
        var code = AccountTokens.Create();
        var codeHash = hasher.Hash(code);
        var email = EmailAddress.Create(command.Email);
        var user = email.IsSuccess ? await users.GetByEmailAsync(email.Value, ct) : null;
        if (
            user is null
            || string.IsNullOrEmpty(user.PasswordHash)
            || user.IsBlocked
            || user.IsDependent
        )
        {
            return Result.Success();
        }

        var now = clock.UtcNow;
        if (user.IsPasswordResetResendCoolingDown(now, passwordReset.ResendCooldown))
        {
            return Result.Success();
        }

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
            logger.EmailSendFailed(EmailKind.PasswordReset, ex);
            return Result.Success();
        }

        user.IssuePasswordResetCode(codeHash, now, passwordReset.CodeLifetime);
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
