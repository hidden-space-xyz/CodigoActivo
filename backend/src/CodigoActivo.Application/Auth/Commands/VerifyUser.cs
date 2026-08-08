using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Auth.Commands;

public sealed record VerifyUserCommand(Guid UserId, string Otp) : ICommand<Result<UserResponse>>;

public sealed class VerifyUserCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    OtpValidator otpValidator
) : ICommandHandler<VerifyUserCommand, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> HandleAsync(
        VerifyUserCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (
            user.UserStatusTypeId != SeedIds.UserStatusTypes.Pending
            || !otpValidator.IsCodeValid(command.Otp, user.OtpCodeHash, user.OtpExpiresAt)
        )
        {
            return Error.BadRequest(ErrorCode.OtpInvalidOrExpired);
        }

        user.Verify(SeedIds.UserStatusTypes.Active, clock.UtcNow);
        await uow.SaveChangesAsync(ct);

        var updated = await users.GetByIdWithDetailsAsync(command.UserId, ct);
        return updated!.ToResponse();
    }
}
