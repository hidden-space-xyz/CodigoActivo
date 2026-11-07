using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Messaging;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Records that wrong passwords locked an account or wrong codes locked its second factor once the
/// lock is committed, so a lock that rolls back is never recorded and a retried one is recorded
/// once: in the security log, without the account, and in the audit log, which names it with the
/// caller that caused the lock.
/// </summary>
/// <param name="passwordLockout">Password lockout configuration, whose limit the record names.</param>
/// <param name="twoFactor">Second-factor configuration, whose limit the record names.</param>
/// <param name="logger">Logger of the lockouts.</param>
/// <param name="audit">Writer of the audit entries.</param>
public sealed class AccountLockoutLog(
    PasswordLockoutOptions passwordLockout,
    TwoFactorOptions twoFactor,
    ILogger<AccountLockoutLog> logger,
    AuditWriter audit
) : IDomainEventListener<PasswordLocked>, IDomainEventListener<TwoFactorLocked>
{
    /// <inheritdoc />
    public Task HandleAsync(PasswordLocked domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        logger.PasswordLockoutTriggered(passwordLockout.MaxFailedAttempts);
        audit.Write(
            new AuditDraft(AuditAction.PasswordLocked, AuditOutcome.Succeeded)
            {
                Message = domainEvent,
            }
        );
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HandleAsync(TwoFactorLocked domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        logger.TwoFactorLockoutTriggered(twoFactor.MaxFailedAttempts);
        audit.Write(
            new AuditDraft(AuditAction.TwoFactorLocked, AuditOutcome.Succeeded)
            {
                Message = domainEvent,
            }
        );
        return Task.CompletedTask;
    }
}
