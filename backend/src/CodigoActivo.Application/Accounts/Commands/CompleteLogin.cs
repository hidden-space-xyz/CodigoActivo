using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the second factor that completes a login.
/// </summary>
/// <param name="Challenge">Challenge the password step opened, named by the challenge cookie.</param>
/// <param name="Code">Code from the email or the authenticator application.</param>
public sealed record CompleteLoginCommand(
    LoginChallengeKey Challenge,
    [property: Required, MaxLength(16), NotBlank] string Code
) : ICommand<Result<UserSessionId>>;

/// <summary>
/// Executes the second step of the login and opens the session in the same transaction, so a login
/// is either complete with its session or not at all. The challenge must be the one the account has
/// open and must not have expired; an account that can no longer sign in or that wrong passwords
/// locked is refused like an expired challenge, before its code is checked. Wrong codes are counted and lock the account's second factor for a while
/// once the limit is reached, so short codes cannot be brute forced. Each check runs with the
/// account row locked and reloaded, so parallel attempts are counted one after another and a code
/// or an authenticator time step is accepted only once.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="sessions">Repository used to store the new session.</param>
/// <param name="uow">Unit of work that runs the attempt in one transaction.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="otpValidator">Validator of emailed codes.</param>
/// <param name="authenticatorCodes">Verifier of authenticator codes.</param>
/// <param name="options">Second-factor configuration.</param>
/// <param name="sessionLifetime">Lifetime applied to the new session.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class CompleteLoginCommandHandler(
    IUserRepository users,
    IUserSessionRepository sessions,
    IUnitOfWork uow,
    IClock clock,
    OtpValidator otpValidator,
    AuthenticatorCodeVerifier authenticatorCodes,
    TwoFactorOptions options,
    SessionLifetimeOptions sessionLifetime,
    ILogger<CompleteLoginCommandHandler> logger
) : ICommandHandler<CompleteLoginCommand, Result<UserSessionId>>
{
    /// <summary>
    /// Handles the request to complete the login.
    /// </summary>
    /// <param name="command">Command containing the challenge and the code.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the new session, or the reason the login was refused.</returns>
    public async Task<Result<UserSessionId>> HandleAsync(
        CompleteLoginCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.GetByIdAsync(command.Challenge.UserId, ct);
        if (user is null)
        {
            return Error.Unauthorized(ApplicationErrorCode.TwoFactorChallengeExpired);
        }

        return await uow.ExecuteInTransactionAsync(
            attempt => CompleteLockedAsync(user, command, attempt),
            ct
        );
    }

    private async Task<Result<UserSessionId>> CompleteLockedAsync(
        User user,
        CompleteLoginCommand command,
        CancellationToken ct
    )
    {
        var now = clock.UtcNow;
        if (
            !await users.LockAsync(user, ct)
            || !user.HasOpenLoginChallenge(
                command.Challenge.ChallengeId,
                now,
                options.ChallengeLifetime
            )
        )
        {
            return Error.Unauthorized(ApplicationErrorCode.TwoFactorChallengeExpired);
        }

        if (user.IsTwoFactorLocked(now))
        {
            return Error.Forbidden(DomainErrorCode.TwoFactorLocked);
        }

        long? usedStep = null;
        var accepted = user.TwoFactorMethod switch
        {
            TwoFactorMethod.Authenticator => (
                usedStep = authenticatorCodes.Match(user.AuthenticatorKey, command.Code)
            )
                is { } matched
                && user.AcceptsAuthenticatorStep(matched),
            _ => otpValidator.IsCodeValid(command.Code, user.UsableLoginCodeHash(now)),
        };

        if (!accepted)
        {
            var locked = user.RecordTwoFactorFailure(
                now,
                options.MaxFailedAttempts,
                options.LockoutDuration
            );
            await uow.SaveChangesAsync(ct);
            if (locked)
            {
                logger.TwoFactorLockoutTriggered(options.MaxFailedAttempts);
            }

            return Error.Validation(ApplicationErrorCode.TwoFactorCodeInvalid);
        }

        if (usedStep is { } step)
        {
            user.RecordAuthenticatorStep(step);
        }

        user.CompleteTwoFactorLogin(now);
        await sessions.RemoveExpiredAsync(now, user.Id, ct);
        var session = UserSession.Start(user.Id, now, sessionLifetime.Lifetime);
        await sessions.AddAsync(session, ct);
        await uow.SaveChangesAsync(ct);
        return session.Id;
    }
}
