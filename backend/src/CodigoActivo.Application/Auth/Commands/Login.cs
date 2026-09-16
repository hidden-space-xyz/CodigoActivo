using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;

namespace CodigoActivo.Application.Auth.Commands;

/// <summary>
/// Carries the input required to login.
/// </summary>
/// <param name="Request">Validated client request data.</param>
public sealed record LoginCommand(LoginRequest Request) : ICommand<Result<UserResponse>>;

/// <summary>
/// Executes the command to login.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="credentialTiming">The credential timing value.</param>
/// <param name="verification">The verification value.</param>
public sealed class LoginCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    CredentialTimingProtector credentialTiming,
    AccountVerificationOptions verification
) : ICommandHandler<LoginCommand, Result<UserResponse>>
{
    /// <summary>
    /// Handles the request to login.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a user on success, or an application error on failure.</returns>
    public async Task<Result<UserResponse>> HandleAsync(
        LoginCommand command,
        CancellationToken ct = default
    )
    {
        var identifier = command.Request.Identifier.Trim();
        var user = await users.GetByEmailOrPhoneAsync(identifier, ct);

        if (
            user is null
            || !credentialTiming.Verify(
                command.Request.Password,
                string.IsNullOrEmpty(user.PasswordHash) ? null : user.PasswordHash
            )
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
