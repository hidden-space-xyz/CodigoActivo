using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;

namespace CodigoActivo.Application.Auth.Commands;

public sealed record LoginCommand(LoginRequest Request) : ICommand<Result<UserResponse>>;

public sealed class LoginCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    AccountVerificationOptions verification
) : ICommandHandler<LoginCommand, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> HandleAsync(
        LoginCommand command,
        CancellationToken ct = default
    )
    {
        var identifier = command.Request.Identifier.Trim();
        var user = await users.GetByEmailOrPhoneAsync(identifier, ct);

        if (
            user is null
            || string.IsNullOrEmpty(user.PasswordHash)
            || !hasher.Verify(command.Request.Password, user.PasswordHash)
        )
        {
            return Error.Unauthorized(ErrorCode.InvalidCredentials);
        }

        if (user.UserStatusTypeId == SeedIds.UserStatusTypes.Blocked)
        {
            return Error.Forbidden(ErrorCode.UserAccountBlocked);
        }

        if (user.UserStatusTypeId == SeedIds.UserStatusTypes.Dependent)
        {
            return Error.Forbidden(ErrorCode.UserAccountIsDependent);
        }

        var selfHealed = false;
        if (user.UserStatusTypeId == SeedIds.UserStatusTypes.Pending)
        {
            if (verification.Required)
            {
                return Error.Forbidden(ErrorCode.UserAccountPendingVerification);
            }

            user.Verify(SeedIds.UserStatusTypes.Active, clock.UtcNow);
            selfHealed = true;
        }

        user.RegisterLogin(clock.UtcNow);
        await uow.SaveChangesAsync(ct);

        return selfHealed
            ? (Result<UserResponse>)(await users.GetByIdWithDetailsAsync(user.Id, ct))!.ToResponse()
            : (Result<UserResponse>)user.ToResponse();
    }
}
