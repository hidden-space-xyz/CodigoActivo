using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Sessions;

/// <summary>
/// Signs an account out everywhere once its new password or its password lock is committed, so a
/// session opened with the old password cannot outlive it and a locked account keeps none open.
/// </summary>
/// <param name="sessions">Repository used to persist and retrieve sessions.</param>
public sealed class SessionsEndOnPasswordReplacedOrLocked(IUserSessionRepository sessions)
    : IDomainEventListener<PasswordChanged>,
        IDomainEventListener<PasswordReset>,
        IDomainEventListener<PasswordLocked>
{
    /// <inheritdoc />
    public Task HandleAsync(PasswordChanged domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return sessions.EndAllAsync(domainEvent.UserId, ct);
    }

    /// <inheritdoc />
    public Task HandleAsync(PasswordReset domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return sessions.EndAllAsync(domainEvent.UserId, ct);
    }

    /// <inheritdoc />
    public Task HandleAsync(PasswordLocked domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return sessions.EndAllAsync(domainEvent.UserId, ct);
    }
}
