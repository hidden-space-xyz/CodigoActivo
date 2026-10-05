using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Tells the owner of an account that its authentication data changed. Handlers call it after a
/// successful commit: a delivery failure, including a refusal from the message limiter, is logged
/// and swallowed, so it never undoes the change it reports.
/// </summary>
/// <param name="emailSender">The email sender value.</param>
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
    /// <param name="user">The user value.</param>
    /// <param name="change">The change value.</param>
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
    /// <param name="previousEmail">Address the account had before the change.</param>
    /// <param name="recipientName">The recipient name value.</param>
    /// <param name="newEmail">Address the account has now when it changed, quoted masked.</param>
    /// <param name="phonesChanged">Whether the phone or the secondary phone changed.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task NotifyIdentifiersChangedAsync(
        string? previousEmail,
        string recipientName,
        string? newEmail,
        bool phonesChanged,
        CancellationToken ct
    )
    {
        var change = (newEmail, phonesChanged) switch
        {
            (null, _) => AccountSecurityChange.PhoneChanged,
            (_, true) => AccountSecurityChange.EmailAndPhoneChanged,
            _ => AccountSecurityChange.EmailChanged,
        };
        return SendAsync(previousEmail, recipientName, change, newEmail?.MaskEmail(), ct);
    }

    private async Task SendAsync(
        string? address,
        string recipientName,
        AccountSecurityChange change,
        string? maskedNewEmail,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        try
        {
            var message = composer.SecurityAlert(
                new EmailRecipient(address, recipientName),
                change,
                clock.UtcNow,
                maskedNewEmail
            );
            await emailSender.SendAsync(message, ct);
        }
        catch (EmailRateLimitedException)
        {
            logger.SecurityNotificationRateLimited(change);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailSendFailed(EmailKind.SecurityAlert, ex);
        }
    }
}
