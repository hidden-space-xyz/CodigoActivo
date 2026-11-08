using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Notifications;

/// <summary>
/// Tells the holder of an account, once the change is committed, that its credentials, its password
/// lock, its second factor or its lock, its staff role, its data protection designation or the
/// identifiers it is reached at changed.
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
        IDomainEventListener<TwoFactorLocked>,
        IDomainEventListener<AuthenticatorEnabled>,
        IDomainEventListener<AuthenticatorDisabled>,
        IDomainEventListener<TwoFactorReset>,
        IDomainEventListener<StaffRoleChanged>,
        IDomainEventListener<PrivacyOfficerChanged>,
        IDomainEventListener<ContactDetailsReplaced>
{
    /// <inheritdoc />
    public Task HandleAsync(PasswordChanged domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return NotifyAsync(domainEvent.UserId, AccountSecurityChange.CredentialChanged, ct);
    }

    /// <inheritdoc />
    public Task HandleAsync(PasswordReset domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return NotifyAsync(domainEvent.UserId, AccountSecurityChange.CredentialReset, ct);
    }

    /// <inheritdoc />
    public Task HandleAsync(PasswordLocked domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return NotifyAsync(domainEvent.UserId, AccountSecurityChange.CredentialLocked, ct);
    }

    /// <inheritdoc />
    public Task HandleAsync(TwoFactorLocked domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return NotifyAsync(domainEvent.UserId, AccountSecurityChange.TwoFactorLocked, ct);
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
    public Task HandleAsync(StaffRoleChanged domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return NotifyAsync(
            domainEvent.UserId,
            domainEvent.StaffRole switch
            {
                StaffRole.Editor => AccountSecurityChange.EditorGranted,
                StaffRole.Administrator => AccountSecurityChange.AdministratorGranted,
                _ => AccountSecurityChange.StaffRoleRevoked,
            },
            ct
        );
    }

    /// <inheritdoc />
    public Task HandleAsync(PrivacyOfficerChanged domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return NotifyAsync(
            domainEvent.UserId,
            domainEvent.IsPrivacyOfficer
                ? AccountSecurityChange.PrivacyOfficerDesignated
                : AccountSecurityChange.PrivacyOfficerReleased,
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
