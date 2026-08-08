using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;

namespace CodigoActivo.Application.Auth.Commands;

public sealed record ResendVerificationCommand(Guid UserId) : ICommand<Result>;

public sealed class ResendVerificationCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    AccountVerificationOptions verification,
    AccountEmails accountEmails
) : ICommandHandler<ResendVerificationCommand, Result>
{
    public async Task<Result> HandleAsync(
        ResendVerificationCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (
            !verification.Required
            || user.UserStatusTypeId != SeedIds.UserStatusTypes.Pending
            || string.IsNullOrWhiteSpace(user.Email)
        )
        {
            return Error.Conflict(ErrorCode.OtpResendNotAllowed);
        }

        var now = clock.UtcNow;
        if (now < user.OtpLastSentAt + verification.ResendCooldown)
        {
            return Error.Conflict(ErrorCode.OtpResendCooldownActive);
        }

        var otpCode = Guid.NewGuid().ToString();
        try
        {
            await accountEmails.SendVerificationEmailAsync(user, otpCode, ct);
        }
        catch (EmailRateLimitedException)
        {
            return Error.Conflict(ErrorCode.OtpResendCooldownActive);
        }

        user.IssueOtp(hasher.Hash(otpCode), now, verification.OtpLifetime);
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
