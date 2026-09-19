using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Auth;

/// <summary>
/// Owns every check of an account password used for authentication: the login password step and
/// the routes that ask the acting caller to re-enter their own password. Consecutive failures are
/// counted on the account and committed even when the calling handler returns without saving;
/// reaching the limit locks the account, closes its pending second-factor challenge, deletes its
/// open sessions and warns its owner, and only a completed password reset clears the lock. A locked
/// account refuses every password, so a correct one cannot be told apart from a wrong one.
/// </summary>
/// <param name="sessions">Repository used to revoke the open sessions of the user.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="hasher">Hasher used to verify a stored password.</param>
/// <param name="credentialTiming">Verifier that pays the same work for accounts without a password.</param>
/// <param name="options">Lockout configuration.</param>
/// <param name="securityNotifier">Notifier that warns the owner about credential changes.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class PasswordAttemptGuard(
    IUserSessionRepository sessions,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    CredentialTimingProtector credentialTiming,
    PasswordLockoutOptions options,
    AccountSecurityNotifier securityNotifier,
    ILogger<PasswordAttemptGuard> logger
)
{
    /// <summary>
    /// Verifies the password of the login step. An identifier matching no account still pays the
    /// same Argon2 work, and so does a locked account, so neither the existence of the account nor
    /// its lock changes the response or its timing. Accounts that store no password, such as
    /// dependent minors, are refused without counting anything: they can never sign in and cannot
    /// recover a password either.
    /// </summary>
    /// <param name="user">Account the identifier resolved to, or <see langword="null"/>.</param>
    /// <param name="password">Plain-text password presented by the client.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the login may continue.</returns>
    public async Task<bool> VerifyLoginPasswordAsync(
        User? user,
        string password,
        CancellationToken ct = default
    )
    {
        var accepted = credentialTiming.Verify(
            password,
            string.IsNullOrEmpty(user?.PasswordHash) ? null : user.PasswordHash
        );

        if (user is null || string.IsNullOrEmpty(user.PasswordHash) || user.IsPasswordLocked())
        {
            return false;
        }

        if (!accepted)
        {
            await RecordFailureAsync(user, ct);
            return false;
        }

        user.ClearPasswordFailures();
        return true;
    }

    /// <summary>
    /// Verifies the password an already signed-in caller re-enters to authorize a change. A locked
    /// account is refused; sessions are deleted when an account locks, so this normally cannot be
    /// reached with a lock in place.
    /// </summary>
    /// <param name="actingUser">Account whose password is being proved, or <see langword="null"/>.</param>
    /// <param name="password">Plain-text password presented by the client.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the caller proved the password.</returns>
    public async Task<bool> VerifyReauthenticationAsync(
        User? actingUser,
        string? password,
        CancellationToken ct = default
    )
    {
        if (
            actingUser is null
            || string.IsNullOrEmpty(actingUser.PasswordHash)
            || string.IsNullOrEmpty(password)
            || actingUser.IsPasswordLocked()
        )
        {
            return false;
        }

        if (!hasher.Verify(password, actingUser.PasswordHash))
        {
            await RecordFailureAsync(actingUser, ct);
            return false;
        }

        actingUser.ClearPasswordFailures();
        return true;
    }

    private async Task RecordFailureAsync(User user, CancellationToken ct)
    {
        var locked = user.RecordPasswordFailure(clock.UtcNow, options.MaxFailedAttempts);
        if (locked)
        {
            user.ClearLoginChallenge();
        }

        await uow.SaveChangesAsync(ct);
        if (!locked)
        {
            return;
        }

        await sessions.RemoveAsync(session => session.UserId == user.Id, ct);
        logger.PasswordLockoutTriggered(user.Id, options.MaxFailedAttempts);
        await securityNotifier.NotifyAsync(user, AccountSecurityChange.PasswordLocked, ct);
    }
}
