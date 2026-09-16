using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to change password.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record ChangePasswordCommand(Guid UserId, ChangePasswordRequest Request)
    : ICommand<Result>;

/// <summary>
/// Executes the command to change password.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="hasher">The hasher value.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class ChangePasswordCommandHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IClock clock,
    IUnitOfWork uow
) : ICommandHandler<ChangePasswordCommand, Result>
{
    /// <summary>
    /// Handles the request to change password.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        ChangePasswordCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return Error.BadRequest(ErrorCode.UserPasswordNotSet);
        }

        if (!hasher.Verify(command.Request.CurrentPassword, user.PasswordHash))
        {
            return Error.BadRequest(ErrorCode.UserCurrentPasswordIncorrect);
        }

        user.ResetPassword(hasher.Hash(command.Request.NewPassword), clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
