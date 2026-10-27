using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.EmailAddresses;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts.Notifications;

/// <summary>
/// Tells the owner of an account about a security-relevant change to it: its password or password
/// lock, its second factor, its administrator rights, or the email and phones it is reached at.
/// <see cref="AccountSecurityNotifications"/> calls it once the change is committed: the alert is
/// always queued, replacing a pending alert about the same kind of change, and a failure to queue it
/// is logged and swallowed, so it never undoes the change it reports.
/// </summary>
/// <param name="emailSender">Sender that queues the alert.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="composer">Composer that renders the security alert.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class AccountSecurityNotifier(
    IEmailSender emailSender,
    IClock clock,
    IAccountEmailComposer composer,
    ILogger<AccountSecurityNotifier> logger
)
{
    /// <summary>
    /// Notifies the account owner at their current address. Accounts without one are skipped.
    /// </summary>
    /// <param name="user">Account whose owner is told; greeted by name only once the address is verified.</param>
    /// <param name="change">Change the alert reports.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task NotifyAsync(User user, AccountSecurityChange change, CancellationToken ct)
    {
        return SendAsync(
            user.Email,
            AccountEmails.RecipientName(user),
            change,
            maskedNewEmail: null,
            ct
        );
    }

    /// <summary>
    /// Notifies the address the account used before its email or phones were replaced, naming what
    /// changed. The new address is only ever quoted masked.
    /// </summary>
    /// <param name="previousEmail">Address the account had before the change; without one nothing is sent.</param>
    /// <param name="recipientName">Name the message greets the holder with.</param>
    /// <param name="newEmail">
    /// Address the account has now when it changed, quoted masked; <see langword="null"/> when only
    /// the phones changed.
    /// </param>
    /// <param name="phonesChanged">Whether the phone or the secondary phone changed.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task NotifyIdentifiersChangedAsync(
        EmailAddress? previousEmail,
        string recipientName,
        EmailAddress? newEmail,
        bool phonesChanged,
        CancellationToken ct
    )
    {
        var change = (newEmail, phonesChanged) switch
        {
            (null, _) => AccountSecurityChange.PhoneChanged,
            (_, true) => AccountSecurityChange.AddressAndPhoneChanged,
            _ => AccountSecurityChange.AddressChanged,
        };
        return SendAsync(previousEmail, recipientName, change, newEmail.MaskEmail(), ct);
    }

    private async Task SendAsync(
        EmailAddress? address,
        string recipientName,
        AccountSecurityChange change,
        string? maskedNewEmail,
        CancellationToken ct
    )
    {
        if (address is null)
        {
            return;
        }

        try
        {
            var message = composer.SecurityAlert(
                new EmailRecipient(address.Value, recipientName),
                change,
                clock.UtcNow,
                maskedNewEmail
            );
            await emailSender.SendAsync(
                message with
                {
                    Topic = EmailTopics.SecurityAlert(change),
                },
                ct
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailSendFailed(EmailKind.SecurityAlert, ex);
        }
    }
}
