using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Notifications;

/// <summary>
/// Tells the holder of an account, once the change is committed, that its credentials, its password
/// lock, its second factor, its administrator rights or the identifiers it is reached at changed.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="notifier">Sender of the account security notifications.</param>
public sealed class AccountSecurityNotifications(
    IUserRepository users,
    AccountSecurityNotifier notifier
)
    : IDomainEventListener<PasswordChanged>,
        IDomainEventListener<PasswordReset>,
        IDomainEventListener<PasswordLocked>,
        IDomainEventListener<AuthenticatorEnabled>,
        IDomainEventListener<AuthenticatorDisabled>,
        IDomainEventListener<TwoFactorReset>,
        IDomainEventListener<AdministratorRightsChanged>,
        IDomainEventListener<ContactDetailsReplaced>
{
    /// <inheritdoc />
    public Task HandleAsync(PasswordChanged domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return NotifyAsync(domainEvent.UserId, AccountSecurityChange.PasswordChanged, ct);
    }

    /// <inheritdoc />
    public Task HandleAsync(PasswordReset domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return NotifyAsync(domainEvent.UserId, AccountSecurityChange.PasswordReset, ct);
    }

    /// <inheritdoc />
    public Task HandleAsync(PasswordLocked domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return NotifyAsync(domainEvent.UserId, AccountSecurityChange.PasswordLocked, ct);
    }

    /// <inheritdoc />
    public Task HandleAsync(AuthenticatorEnabled domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return NotifyAsync(domainEvent.UserId, AccountSecurityChange.AuthenticatorEnabled, ct);
    }

    /// <inheritdoc />
    public Task HandleAsync(AuthenticatorDisabled domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return NotifyAsync(domainEvent.UserId, AccountSecurityChange.AuthenticatorDisabled, ct);
    }

    /// <inheritdoc />
    public Task HandleAsync(TwoFactorReset domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return NotifyAsync(domainEvent.UserId, AccountSecurityChange.TwoFactorReset, ct);
    }

    /// <inheritdoc />
    public Task HandleAsync(AdministratorRightsChanged domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return NotifyAsync(
            domainEvent.UserId,
            domainEvent.IsAdmin
                ? AccountSecurityChange.AdminGranted
                : AccountSecurityChange.AdminRevoked,
            ct
        );
    }

    /// <inheritdoc />
    public Task HandleAsync(ContactDetailsReplaced domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return notifier.NotifyIdentifiersChangedAsync(
            domainEvent.PreviousEmail,
            domainEvent.FirstName,
            domainEvent.NewEmail,
            domainEvent.PhonesReplaced,
            ct
        );
    }

    private async Task NotifyAsync(
        UserId userId,
        AccountSecurityChange change,
        CancellationToken ct
    )
    {
        var user = await users.GetByIdAsync(userId, ct);
        if (user is not null)
        {
            await notifier.NotifyAsync(user, change, ct);
        }
    }
}
