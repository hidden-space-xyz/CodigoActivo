using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to email a new login code for an open challenge.
/// </summary>
/// <param name="Challenge">Challenge the password step opened, named by the challenge cookie.</param>
public sealed record ResendTwoFactorCodeCommand(LoginChallengeKey Challenge) : ICommand<Result>;

/// <summary>
/// Executes the command to email a new login code, replacing the previous one, while the challenge
/// it answers is still open and the account may still sign in.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="options">Second-factor configuration.</param>
/// <param name="loginCodes">Issuer of emailed login codes.</param>
public sealed class ResendTwoFactorCodeCommandHandler(
    IUserRepository users,
    IClock clock,
    TwoFactorOptions options,
    LoginCodeIssuer loginCodes
) : ICommandHandler<ResendTwoFactorCodeCommand, Result>
{
    /// <summary>
    /// Handles the request to resend the login code.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        ResendTwoFactorCodeCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.UtcNow;
        var user = await users.GetByIdAsync(command.Challenge.UserId, ct);
        if (
            user is null
            || !user.HasOpenLoginChallenge(
                command.Challenge.ChallengeId,
                now,
                options.ChallengeLifetime
            )
        )
        {
            return Error.Unauthorized(ApplicationErrorCode.TwoFactorChallengeExpired);
        }

        var allowed = user.MayReceiveLoginCode(now);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        if (user.IsLoginCodeResendCoolingDown(now, options.ResendCooldown))
        {
            return Error.Conflict(ApplicationErrorCode.TwoFactorResendCooldownActive);
        }

        return await loginCodes.IssueAsync(user, now, ct);
    }
}
