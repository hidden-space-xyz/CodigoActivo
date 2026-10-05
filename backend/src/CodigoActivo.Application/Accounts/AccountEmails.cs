using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Builds and sends the transactional emails for account.
/// </summary>
/// <param name="emailSender">The email sender value.</param>
/// <param name="composer">Composer that renders the account messages.</param>
/// <param name="verification">The verification value.</param>
/// <param name="passwordReset">The password reset value.</param>
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
    /// Sends the verification email message to its recipients.
    /// </summary>
    /// <param name="user">The user value.</param>
    /// <param name="otpCode">The otp code value.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SendVerificationEmailAsync(User user, string otpCode, CancellationToken ct)
    {
        var message = composer.AccountVerification(
            Recipient(user),
            user.Id,
            otpCode,
            verification.OtpLifetime
        );
        return emailSender.SendAsync(message, ct);
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
        string newEmail,
        string code,
        CancellationToken ct
    )
    {
        var message = composer.EmailChangeConfirmation(
            new EmailRecipient(newEmail, string.Empty),
            user.Id,
            code,
            verification.OtpLifetime
        );
        return emailSender.SendAsync(message, ct);
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
        return emailSender.SendAsync(composer.EmailInUse(Recipient(holder)), ct);
    }

    /// <summary>
    /// Sends the password reset email message to its recipients.
    /// </summary>
    /// <param name="user">The user value.</param>
    /// <param name="code">The code value.</param>
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
        return emailSender.SendAsync(message, ct);
    }

    /// <summary>
    /// Sends the email carrying the one-time code that completes a login.
    /// </summary>
    /// <param name="user">The user value.</param>
    /// <param name="code">Plain code the user must type; it is never stored.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SendLoginCodeEmailAsync(User user, string code, CancellationToken ct)
    {
        var message = composer.LoginCode(Recipient(user), code, twoFactor.ChallengeLifetime);
        return emailSender.SendAsync(message, ct);
    }

    /// <summary>
    /// Sends the email carrying the one-time code that confirms deleting an account.
    /// </summary>
    /// <param name="user">The user value.</param>
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
        return emailSender.SendAsync(message, ct);
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

    private static EmailRecipient Recipient(User user)
    {
        return new EmailRecipient(user.Email!, RecipientName(user));
    }
}
