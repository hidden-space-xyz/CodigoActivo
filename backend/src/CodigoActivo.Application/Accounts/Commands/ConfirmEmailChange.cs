using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to confirm the new email an account holder asked for.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
/// <param name="Code">Code from the link emailed to the new address.</param>
public sealed record ConfirmEmailChangeCommand(
    UserId UserId,
    [property: Required, MaxLength(128), NotBlank] string Code
) : ICommand<Result>;

/// <summary>
/// Executes the command to confirm the new email an account holder asked for.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="otpValidator">Validator of the code from the link.</param>
/// <param name="emailClaims">Committer that replaces an unverified account holding the address.</param>
public sealed class ConfirmEmailChangeCommandHandler(
    IUserRepository users,
    IClock clock,
    OtpValidator otpValidator,
    EmailClaims emailClaims
) : ICommandHandler<ConfirmEmailChangeCommand, Result>
{
    /// <summary>
    /// Handles the request to confirm the new email. The code proves that whoever asked for the
    /// change controls the new address, so the account moves to it and the previous address is
    /// told, quoting the new one masked. An account nobody verified that held the address is
    /// erased in the same transaction, while one that <see cref="User.OwnsEmail"/>, verified since
    /// the link was sent, keeps it.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        ConfirmEmailChangeCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        var now = clock.UtcNow;
        if (
            user.PendingEmail is not { } newEmail
            || !otpValidator.IsCodeValid(command.Code, user.UsableEmailChangeCodeHash(now))
        )
        {
            return Error.Validation(ApplicationErrorCode.OtpInvalidOrExpired);
        }

        var holder = await users.GetByEmailAsync(newEmail, ct);
        if (holder is { OwnsEmail: true })
        {
            return Error.Conflict(ApplicationErrorCode.UserEmailAlreadyInUse);
        }

        user.ConfirmEmailChange(now);
        if (!await emailClaims.TryCommitAsync(user, holder, now, ct))
        {
            return Error.Conflict(ApplicationErrorCode.UserEmailAlreadyInUse);
        }

        return Result.Success();
    }
}
