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

    private static EmailRecipient Recipient(User user)
    {
        return new EmailRecipient(user.Email!, user.FirstName);
    }
}
