using CodigoActivo.Application.Diagnostics;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Auth;

/// <summary>
/// Tells the owner of an account that its authentication data changed. Handlers call it after a
/// successful commit: a delivery failure, including a refusal from the message limiter, is logged
/// and swallowed, so it never undoes the change it reports.
/// </summary>
/// <param name="emailSender">The email sender value.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="application">The application value.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class AccountSecurityNotifier(
    IEmailSender emailSender,
    IClock clock,
    ApplicationOptions application,
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
        return SendAsync(user.Email, user.FirstName, change, maskedNewEmail: null, ct);
    }

    /// <summary>
    /// Notifies the address the account used before its login identifiers were replaced. The new
    /// address is only ever quoted masked.
    /// </summary>
    /// <param name="previousEmail">Address the account had before the change.</param>
    /// <param name="recipientName">The recipient name value.</param>
    /// <param name="newEmail">Address the account has now, quoted masked or not at all.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task NotifyIdentifiersChangedAsync(
        string? previousEmail,
        string recipientName,
        string? newEmail,
        CancellationToken ct
    )
    {
        return SendAsync(
            previousEmail,
            recipientName,
            AccountSecurityChange.IdentifiersChanged,
            newEmail?.MaskEmail(),
            ct
        );
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
            var message = SecurityAlertEmail.Create(
                address,
                recipientName,
                change,
                clock.UtcNow,
                clock.TimeZone,
                application.BaseUrl.TrimEnd('/'),
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
