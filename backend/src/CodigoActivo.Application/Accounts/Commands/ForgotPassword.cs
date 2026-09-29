using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to forgot password.
/// </summary>
/// <param name="Request">Validated client request data.</param>
public sealed record ForgotPasswordCommand(ForgotPasswordRequest Request) : ICommand<Result>;

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
    /// Handles the request to forgot password.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        ForgotPasswordCommand command,
        CancellationToken ct = default
    )
    {
        var email = command.Request.Email.NormalizeEmailOrNull();
        if (email is null)
        {
            return Result.Success();
        }

        var user = await users.GetByEmailAsync(email, ct);
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
            logger.EmailSendFailed(EmailKind.PasswordReset, ex);
            return Result.Success();
        }

        user.IssuePasswordResetCode(hasher.Hash(code), now, passwordReset.CodeLifetime);
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
