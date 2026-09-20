using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Auth.Commands;

/// <summary>
/// Carries the input required to verify user.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="Otp">The otp value.</param>
public sealed record VerifyUserCommand(Guid UserId, string Otp) : ICommand<Result<UserResponse>>;

/// <summary>
/// Executes the command to verify user.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="otpValidator">The otp validator value.</param>
public sealed class VerifyUserCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    OtpValidator otpValidator
) : ICommandHandler<VerifyUserCommand, Result<UserResponse>>
{
    /// <summary>
    /// Handles the request to verify user.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a user on success, or an application error on failure.</returns>
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
