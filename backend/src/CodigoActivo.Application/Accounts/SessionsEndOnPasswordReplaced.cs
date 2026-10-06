using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Signs an account out everywhere once its new password is committed, so a session opened with
/// the old one cannot outlive it.
/// </summary>
/// <param name="sessions">Repository used to persist and retrieve sessions.</param>
public sealed class SessionsEndOnPasswordReplaced(IUserSessionRepository sessions)
    : IDomainEventListener<PasswordChanged>,
        IDomainEventListener<PasswordReset>
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
}
