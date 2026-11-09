using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Passwords;

/// <summary>
/// Owns every check of an account password used for authentication: the login password step and
/// the routes that ask the acting caller to re-enter their own password. Every wrong password is
/// counted inside a transaction that holds the account row locked, so parallel attempts cannot
/// lose each other's increments; reaching the limit locks the account for
/// <see cref="PasswordLockoutOptions.LockoutDuration"/> and closes its pending second-factor
/// challenge, and a completed password reset lifts the lock earlier. Open sessions stay open. The
/// lock raises <see cref="PasswordLocked"/> exactly once, and its listeners record the lockout and
/// warn the owner once it is committed. A correct password re-reads the lockout
/// state with the row locked the same way, so a lock committed after the account was loaded still
/// refuses it, and commits the reset of the counter there, because the calling handler may return
/// without saving. The guard never holds the row locked while it verifies a password, and callers
/// must not call it with the row locked, so the slow hashing never holds back other requests for
/// the account. A locked account refuses every password, so a correct one cannot be told apart from
/// a wrong one.
/// </summary>
/// <param name="users">
/// Repository that counts the failures and locks the account atomically.
/// </param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="hasher">Hasher used to verify a stored password.</param>
/// <param name="credentialTiming">
/// Verifier that pays the same work for accounts without a password.
/// </param>
/// <param name="options">Lockout configuration.</param>
public sealed class PasswordAttemptGuard(
    IUserRepository users,
    ICurrentUser currentUser,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    CredentialTimingProtector credentialTiming,
    PasswordLockoutOptions options
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
    /// <returns>
    /// A task whose result is <see langword="true"/> when the login may continue.
    /// </returns>
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

        if (
            user is null
            || string.IsNullOrEmpty(user.PasswordHash)
            || user.PasswordLockout.IsLocked(clock.UtcNow)
        )
        {
            return false;
        }

        if (!accepted)
        {
            await RecordFailureAsync(user, ct);
            return false;
        }

        return await ForgetFailuresAsync(user, ct);
    }

    /// <summary>
    /// Verifies the password an already signed-in caller re-enters to authorize a change. While the
    /// account is locked every password is refused without counting it: the lock leaves the
    /// sessions open, so their holder waits for it to expire or resets the password.
    /// </summary>
    /// <param name="actingUser">
    /// Account whose password is being proved, or <see langword="null"/>.
    /// </param>
    /// <param name="password">Plain-text password presented by the client.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is <see langword="true"/> when the caller proved the password.
    /// </returns>
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
            || actingUser.PasswordLockout.IsLocked(clock.UtcNow)
        )
        {
            return false;
        }

        if (!hasher.Verify(password, actingUser.PasswordHash))
        {
            await RecordFailureAsync(actingUser, ct);
            return false;
        }

        return await ForgetFailuresAsync(actingUser, ct);
    }

    /// <summary>
    /// Verifies the password the signed-in caller re-enters to authorize a change, loading the
    /// caller's own account first.
    /// </summary>
    /// <param name="password">Plain-text password presented by the client.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is <see langword="true"/> when the caller proved the password.
    /// </returns>
    public async Task<bool> VerifyActingUserAsync(string? password, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var actingUser = await users.GetByIdAsync(currentUser.RequiredId(), ct);
        return await VerifyReauthenticationAsync(actingUser, password, ct);
    }

    /// <summary>
    /// Tells whether an account reloaded with its row locked still stores the password that was
    /// verified and is not locked by wrong passwords, so a password replaced or an account locked
    /// since the check is refused.
    /// </summary>
    /// <param name="user">Account reloaded with its row locked.</param>
    /// <param name="verifiedHash">Stored password hash the password was verified against.</param>
    /// <param name="now">Current timestamp.</param>
    /// <returns><see langword="true"/> when the verified password still applies.</returns>
    public static bool StillVerified(User user, string? verifiedHash, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(user);

        return string.Equals(user.PasswordHash, verifiedHash, StringComparison.Ordinal)
            && !user.PasswordLockout.IsLocked(now);
    }

    private async Task<bool> ForgetFailuresAsync(User user, CancellationToken ct)
    {
        return await uow.ExecuteInTransactionAsync(
            async attempt =>
            {
                if (
                    !await users.LockPasswordStateAsync(user, attempt)
                    || user.PasswordLockout.IsLocked(clock.UtcNow)
                )
                {
                    return false;
                }

                if (user.PasswordLockout.FailedAttempts > 0)
                {
                    user.ClearPasswordFailures();
                    await users.SavePasswordStateAsync(user, attempt);
                }

                return true;
            },
            ct
        );
    }

    private async Task RecordFailureAsync(User user, CancellationToken ct)
    {
        await uow.ExecuteInTransactionAsync(
            async attempt =>
            {
                var now = clock.UtcNow;
                if (
                    !await users.LockPasswordStateAsync(user, attempt)
                    || user.PasswordLockout.IsLocked(now)
                )
                {
                    return false;
                }

                user.RecordPasswordFailure(now, options.MaxFailedAttempts, options.LockoutDuration);
                await users.SavePasswordStateAsync(user, attempt);
                return true;
            },
            ct
        );
    }
}
