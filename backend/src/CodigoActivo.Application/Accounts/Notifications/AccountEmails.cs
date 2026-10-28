using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Notifications;

/// <summary>
/// Builds the transactional emails of an account and hands them to the email sender, which applies
/// the email guard to the codes and queues them for delivery. Every message names its topic, so a
/// new one replaces the one still waiting, and a code says how long it is valid, so it is never
/// delivered once useless. A refused code throws <see cref="EmailRateLimitedException"/>; callers
/// decide whether that fails their request.
/// </summary>
/// <param name="emailSender">Sender that applies the message limiter and queues the messages.</param>
/// <param name="composer">Composer that renders the account messages.</param>
/// <param name="verification">Account verification configuration, whose code lifetime the verification and email change messages state.</param>
/// <param name="passwordReset">Password reset configuration, whose code lifetime the reset message states.</param>
/// <param name="twoFactor">Second-factor configuration.</param>
public sealed class AccountEmails(
    IEmailSender emailSender,
    IAccountEmailComposer composer,
    AccountVerificationOptions verification,
    PasswordResetOptions passwordReset,
    TwoFactorOptions twoFactor
)
{
    /// <summary>
    /// Sends the code that verifies the email of a new account, mentioning the news consent the
    /// registration gave.
    /// </summary>
    /// <param name="user">Account waiting for its verification; greeted by nobody until it is verified.</param>
    /// <param name="otpCode">Plain verification code; only its hash is stored.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SendVerificationEmailAsync(User user, string otpCode, CancellationToken ct)
    {
        var message = composer.AccountVerification(
            Recipient(user),
            user.Id,
            otpCode,
            verification.OtpLifetime,
            user.NewsConsent
        );
        return emailSender.SendAsync(
            message with
            {
                Topic = EmailTopics.AccountVerification,
                ValidFor = verification.OtpLifetime,
            },
            ct
        );
    }

    /// <summary>
    /// Sends the link that confirms <paramref name="newEmail"/> as the new address of the account.
    /// Nobody has verified that address yet, so the message greets nobody.
    /// </summary>
    /// <param name="user">Account the holder asked to move.</param>
    /// <param name="newEmail">Address the link is sent to.</param>
    /// <param name="code">Plain code the link carries; only its hash is stored.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SendEmailChangeConfirmationAsync(
        User user,
        EmailAddress newEmail,
        string code,
        CancellationToken ct
    )
    {
        var message = composer.EmailChangeConfirmation(
            new EmailRecipient(newEmail.Value, string.Empty),
            user.Id,
            code,
            verification.OtpLifetime
        );
        return emailSender.SendAsync(
            message with
            {
                Topic = EmailTopics.EmailChange(user.Id.Value),
                ValidFor = verification.OtpLifetime,
            },
            ct
        );
    }

    /// <summary>
    /// Tells the holder of <paramref name="holder"/> that someone tried to use its email for another
    /// account. Registrations and email changes send it instead of their link, so the caller cannot
    /// tell whether the address already had an account.
    /// </summary>
    /// <param name="holder">Account that has the address.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SendEmailInUseNoticeAsync(User holder, CancellationToken ct)
    {
        return emailSender.SendAsync(
            composer.EmailInUse(Recipient(holder)) with
            {
                Topic = EmailTopics.EmailInUse,
            },
            ct
        );
    }

    /// <summary>
    /// Sends the code that lets the holder of the account choose a new password.
    /// </summary>
    /// <param name="user">Account whose password is reset; it must have an email.</param>
    /// <param name="code">Plain reset code; only its hash is stored.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SendPasswordResetEmailAsync(User user, string code, CancellationToken ct)
    {
        var message = composer.PasswordReset(
            Recipient(user),
            user.Id,
            code,
            passwordReset.CodeLifetime
        );
        return emailSender.SendAsync(
            message with
            {
                Topic = EmailTopics.PasswordReset,
                ValidFor = passwordReset.CodeLifetime,
            },
            ct
        );
    }

    /// <summary>
    /// Sends the email carrying the one-time code that completes a login.
    /// </summary>
    /// <param name="user">Account signing in; it must have an email.</param>
    /// <param name="code">Plain code the user must type; it is never stored.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SendLoginCodeEmailAsync(User user, string code, CancellationToken ct)
    {
        var message = composer.LoginCode(Recipient(user), code, twoFactor.ChallengeLifetime);
        return SendAccountCodeAsync(message, ct);
    }

    /// <summary>
    /// Sends the email carrying the one-time code that confirms deleting an account.
    /// </summary>
    /// <param name="user">Account to delete; it must have an email.</param>
    /// <param name="code">Plain code the user must type; it is never stored.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SendAccountDeletionCodeEmailAsync(User user, string code, CancellationToken ct)
    {
        var message = composer.AccountDeletionCode(
            Recipient(user),
            code,
            twoFactor.ChallengeLifetime
        );
        return SendAccountCodeAsync(message, ct);
    }

    /// <summary>
    /// Sends the email carrying the one-time code that confirms opening a consultation of the
    /// blocked data.
    /// </summary>
    /// <param name="user">Account designated for data protection; it must have an email.</param>
    /// <param name="code">Plain code the user must type; it is never stored.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SendBlockedDataConsultationCodeEmailAsync(
        User user,
        string code,
        CancellationToken ct
    )
    {
        var message = composer.BlockedDataConsultationCode(
            Recipient(user),
            code,
            twoFactor.ChallengeLifetime
        );
        return SendAccountCodeAsync(message, ct);
    }

    /// <summary>
    /// Tells the guardian of <paramref name="dependent"/> on which day it comes of age and is erased.
    /// </summary>
    /// <param name="guardian">Guardian of the dependent, with a verified email.</param>
    /// <param name="dependent">Dependent about to come of age.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">The dependent has no birth date.</exception>
    public Task SendComingOfAgeNoticeAsync(User guardian, User dependent, CancellationToken ct)
    {
        var birthDate =
            dependent.BirthDate
            ?? throw new ArgumentException("The dependent has no birth date.", nameof(dependent));
        var message = composer.ComingOfAgeNotice(
            Recipient(guardian),
            $"{dependent.FirstName} {dependent.LastName}",
            birthDate.ComingOfAgeOn()
        );
        return emailSender.SendAsync(
            message with
            {
                Topic = EmailTopics.ComingOfAge(dependent.Id.Value),
            },
            ct
        );
    }

    /// <summary>
    /// Gets the name account mail greets its owner with. Anyone may register an address they do not
    /// own, so mail to an address nobody has verified yet greets nobody: the site never relays a
    /// name a stranger typed to someone else's mailbox.
    /// </summary>
    /// <param name="user">Account the mail is about.</param>
    /// <returns>The first name, or an empty name while the address is unverified.</returns>
    internal static string RecipientName(User user)
    {
        return user.IsPendingVerification ? string.Empty : user.FirstName;
    }

    private Task SendAccountCodeAsync(EmailMessage message, CancellationToken ct)
    {
        return emailSender.SendAsync(
            message with
            {
                Topic = EmailTopics.AccountCode,
                ValidFor = twoFactor.ChallengeLifetime,
            },
            ct
        );
    }

    private static EmailRecipient Recipient(User user)
    {
        return new EmailRecipient(user.Email!.Value, RecipientName(user));
    }
}
