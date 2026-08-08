using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Auth.Commands;

public sealed record ForgotPasswordCommand(ForgotPasswordRequest Request) : ICommand<Result>;

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

        var user = await users.FindAsync(u => u.Email == email, ct);
        if (
            user is null
            || string.IsNullOrEmpty(user.PasswordHash)
            || user.UserStatusTypeId == SeedIds.UserStatusTypes.Blocked
            || user.UserStatusTypeId == SeedIds.UserStatusTypes.Dependent
        )
        {
            return Result.Success();
        }

        var now = clock.UtcNow;
        if (now < user.PasswordResetLastSentAt + passwordReset.ResendCooldown)
        {
            return Result.Success();
        }

        var code = Guid.NewGuid().ToString();
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
            logger.LogError(
                ex,
                "Failed to send the password reset email for user {UserId}",
                user.Id
            );
            return Result.Success();
        }

        user.IssuePasswordResetCode(hasher.Hash(code), now, passwordReset.CodeLifetime);
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
