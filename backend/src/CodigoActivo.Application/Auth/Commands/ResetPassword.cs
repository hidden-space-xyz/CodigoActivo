using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;

namespace CodigoActivo.Application.Auth.Commands;

public sealed record ResetPasswordCommand(Guid UserId, ResetPasswordRequest Request)
    : ICommand<Result>;

public sealed class ResetPasswordCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    OtpValidator otpValidator
) : ICommandHandler<ResetPasswordCommand, Result>
{
    public async Task<Result> HandleAsync(
        ResetPasswordCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (
            user.UserStatusTypeId == SeedIds.UserStatusTypes.Blocked
            || user.UserStatusTypeId == SeedIds.UserStatusTypes.Dependent
            || !otpValidator.IsCodeValid(
                request.Otp,
                user.PasswordResetCodeHash,
                user.PasswordResetExpiresAt
            )
        )
        {
            return Error.BadRequest(ErrorCode.PasswordResetInvalidOrExpired);
        }

        user.ResetPassword(hasher.Hash(request.NewPassword), clock.UtcNow);
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
