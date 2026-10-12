using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Records in the security log that wrong passwords locked an account or wrong codes locked its
/// second factor once the lock is committed, so a lock that rolls back is never recorded and a
/// retried one is recorded once.
/// </summary>
/// <param name="passwordLockout">Password lockout configuration, whose limit the record names.</param>
/// <param name="twoFactor">Second-factor configuration, whose limit the record names.</param>
/// <param name="logger">Logger of the lockouts.</param>
public sealed class AccountLockoutLog(
    PasswordLockoutOptions passwordLockout,
    TwoFactorOptions twoFactor,
    ILogger<AccountLockoutLog> logger
) : IDomainEventListener<PasswordLocked>, IDomainEventListener<TwoFactorLocked>
{
    /// <inheritdoc />
    public Task HandleAsync(PasswordLocked domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        logger.PasswordLockoutTriggered(passwordLockout.MaxFailedAttempts);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HandleAsync(TwoFactorLocked domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        logger.TwoFactorLockoutTriggered(twoFactor.MaxFailedAttempts);
        return Task.CompletedTask;
    }
}
