using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Common;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Renders the automatic account messages with the site layout and links them to the pages of the
/// web application that complete each action.
/// </summary>
/// <param name="application">Options that carry the public base URL of the site.</param>
/// <param name="clock">Clock whose time zone dates the security alerts.</param>
public sealed class AccountEmailComposer(ApplicationOptions application, IClock clock)
    : IAccountEmailComposer
{
    private const string VerificationPath = "/verify-account";
    private const string PasswordResetPath = "/reset-password";

    private string SiteUrl => application.BaseUrl.TrimEnd('/');

    /// <inheritdoc />
    public EmailMessage AccountVerification(
        EmailRecipient recipient,
        Guid userId,
        string code,
        TimeSpan lifetime
    )
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return VerificationEmail.Create(
            recipient.Address,
            recipient.Name,
            BuildAccountUrl(VerificationPath, userId, code),
            SiteUrl,
            lifetime
        );
    }

    /// <inheritdoc />
    public EmailMessage PasswordReset(
        EmailRecipient recipient,
        Guid userId,
        string code,
        TimeSpan lifetime
    )
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return PasswordResetEmail.Create(
            recipient.Address,
            recipient.Name,
            BuildAccountUrl(PasswordResetPath, userId, code),
            SiteUrl,
            lifetime
        );
    }

    /// <inheritdoc />
    public EmailMessage LoginCode(EmailRecipient recipient, string code, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return LoginCodeEmail.Create(recipient.Address, recipient.Name, code, SiteUrl, lifetime);
    }

    /// <inheritdoc />
    public EmailMessage AccountDeletionCode(
        EmailRecipient recipient,
        string code,
        TimeSpan lifetime
    )
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return AccountDeletionCodeEmail.Create(
            recipient.Address,
            recipient.Name,
            code,
            SiteUrl,
            lifetime
        );
    }

    /// <inheritdoc />
    public EmailMessage SecurityAlert(
        EmailRecipient recipient,
        AccountSecurityChange change,
        DateTimeOffset occurredAt,
        string? maskedNewEmail
    )
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return SecurityAlertEmail.Create(
            recipient.Address,
            recipient.Name,
            change,
            occurredAt,
            clock.TimeZone,
            SiteUrl,
            maskedNewEmail
        );
    }

    private string BuildAccountUrl(string path, Guid userId, string code)
    {
        return $"{SiteUrl}{path}#userId={userId}&code={Uri.EscapeDataString(code)}";
    }
}
