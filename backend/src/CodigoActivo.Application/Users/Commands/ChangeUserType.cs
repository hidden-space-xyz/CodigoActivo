using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to change the membership type of a user.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="UserType">New membership type.</param>
public sealed record ChangeUserTypeCommand(UserId UserId, UserType UserType) : ICommand<Result>;

/// <summary>
/// Executes the command to change the membership type of a user.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class ChangeUserTypeCommandHandler(IUserRepository users, IClock clock)
    : ICommandHandler<ChangeUserTypeCommand, Result>
{
    /// <summary>
    /// Handles the request to change the membership type of a user.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        ChangeUserTypeCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        if (user.UserType != command.UserType)
        {
            user.ChangeType(command.UserType, clock.UtcNow);
        }

        return Result.Success();
    }
}
