using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Domain.Users;

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
    private const string EmailChangePath = "/confirm-email";
    private const string PasswordResetPath = "/reset-password";
    private const string LoginPath = "/login";
    private const string AccountPath = "/account";

    private string SiteUrl => application.BaseUrl.TrimEnd('/');

    /// <inheritdoc />
    public EmailMessage AccountVerification(
        EmailRecipient recipient,
        UserId userId,
        string code,
        TimeSpan lifetime,
        bool newsConsent
    )
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return ExpiringEmail.Create(
            ExpiringEmail.Verification,
            EmailKind.AccountVerification,
            recipient,
            EmailBlocks.Action(
                AppStrings.EmailsVerificationButtonLabel,
                BuildAccountUrl(VerificationPath, userId, code)
            ),
            SiteUrl,
            lifetime,
            newsConsent ? [EmailBlocks.Prose(AppStrings.EmailsVerificationNewsConsent)] : []
        );
    }

    /// <inheritdoc />
    public EmailMessage EmailChangeConfirmation(
        EmailRecipient recipient,
        UserId userId,
        string code,
        TimeSpan lifetime
    )
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return ExpiringEmail.Create(
            ExpiringEmail.EmailChange,
            EmailKind.AccountVerification,
            recipient,
            EmailBlocks.Action(
                AppStrings.EmailsEmailChangeButtonLabel,
                BuildAccountUrl(EmailChangePath, userId, code)
            ),
            SiteUrl,
            lifetime
        );
    }

    /// <inheritdoc />
    public EmailMessage EmailInUse(EmailRecipient recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return EmailInUseEmail.Create(
            recipient.Address,
            recipient.Name,
            $"{SiteUrl}{LoginPath}",
            SiteUrl
        );
    }

    /// <inheritdoc />
    public EmailMessage PasswordReset(
        EmailRecipient recipient,
        UserId userId,
        string code,
        TimeSpan lifetime
    )
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return ExpiringEmail.Create(
            ExpiringEmail.PasswordReset,
            EmailKind.AccessRecovery,
            recipient,
            EmailBlocks.Action(
                AppStrings.EmailsPasswordResetButtonLabel,
                BuildAccountUrl(PasswordResetPath, userId, code)
            ),
            SiteUrl,
            lifetime
        );
    }

    /// <inheritdoc />
    public EmailMessage LoginCode(EmailRecipient recipient, string code, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return ExpiringEmail.Create(
            ExpiringEmail.LoginCode,
            EmailKind.TwoFactorCode,
            recipient,
            EmailBlocks.Code(code),
            SiteUrl,
            lifetime
        );
    }

    /// <inheritdoc />
    public EmailMessage ConfirmationCode(
        EmailRecipient recipient,
        ConfirmationCodePurpose purpose,
        string code,
        TimeSpan lifetime
    )
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return ExpiringEmail.Create(
            ExpiringEmail.ConfirmationCode(purpose),
            EmailKind.TwoFactorCode,
            recipient,
            EmailBlocks.Code(code),
            SiteUrl,
            lifetime
        );
    }

    /// <inheritdoc />
    public EmailMessage BlockedDataConsultationCode(
        EmailRecipient recipient,
        string code,
        TimeSpan lifetime
    )
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return ExpiringEmail.Create(
            ExpiringEmail.BlockedDataConsultationCode,
            EmailKind.TwoFactorCode,
            recipient,
            EmailBlocks.Code(code),
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

    /// <inheritdoc />
    public EmailMessage ComingOfAgeNotice(
        EmailRecipient recipient,
        string dependentName,
        DateOnly comingOfAgeOn
    )
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return ComingOfAgeNoticeEmail.Create(
            recipient.Address,
            recipient.Name,
            dependentName,
            comingOfAgeOn,
            $"{SiteUrl}{AccountPath}",
            SiteUrl
        );
    }

    private string BuildAccountUrl(string path, UserId userId, string code)
    {
        return $"{SiteUrl}{path}#userId={userId}&code={Uri.EscapeDataString(code)}";
    }
}
