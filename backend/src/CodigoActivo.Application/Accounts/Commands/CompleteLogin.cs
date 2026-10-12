using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

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
/// locked is refused like an expired challenge, before its code is checked. The code is checked by
/// <see cref="SecondFactorGuard"/> with the account row locked and reloaded, and an accepted login
/// clears the challenge, so a code or an authenticator time step is accepted only once.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="sessions">Repository used to store the new session.</param>
/// <param name="uow">Unit of work that runs the attempt in one transaction.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="secondFactor">Guard that verifies, counts and locks second-factor codes.</param>
/// <param name="options">Second-factor configuration.</param>
/// <param name="sessionLifetime">Lifetime applied to the new session.</param>
public sealed class CompleteLoginCommandHandler(
    IUserRepository users,
    IUserSessionRepository sessions,
    IUnitOfWork uow,
    IClock clock,
    SecondFactorGuard secondFactor,
    TwoFactorOptions options,
    SessionLifetimeOptions sessionLifetime
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

        var verified = await secondFactor.VerifyLockedAsync(user, command.Code, now, ct);
        if (verified.IsFailure)
        {
            return verified.Error!;
        }

        user.CompleteTwoFactorLogin(now);
        await sessions.PurgeExpiredAsync(now, user.Id, ct);
        var session = UserSession.Start(user.Id, now, sessionLifetime.Lifetime);
        await sessions.AddAsync(session, ct);
        await uow.SaveChangesAsync(ct);
        return session.Id;
    }
}
