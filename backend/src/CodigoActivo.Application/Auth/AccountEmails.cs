using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.Auth;

/// <summary>
/// Builds and sends the transactional emails for account.
/// </summary>
/// <param name="emailSender">The email sender value.</param>
/// <param name="verification">The verification value.</param>
/// <param name="passwordReset">The password reset value.</param>
/// <param name="application">The application value.</param>
/// <param name="twoFactor">Second-factor configuration.</param>
public sealed class AccountEmails(
    IEmailSender emailSender,
    AccountVerificationOptions verification,
    PasswordResetOptions passwordReset,
    ApplicationOptions application,
    TwoFactorOptions twoFactor
)
{
    private const string VerificationPath = "/verify-account";
    private const string PasswordResetPath = "/reset-password";

    /// <summary>
    /// Sends the verification email message to its recipients.
    /// </summary>
    /// <param name="user">The user value.</param>
    /// <param name="otpCode">The otp code value.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SendVerificationEmailAsync(User user, string otpCode, CancellationToken ct)
    {
        var message = VerificationEmail.Create(
            user.Email!,
            user.FirstName,
            BuildAccountUrl(VerificationPath, user.Id, otpCode),
            BuildSiteUrl(),
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
        var message = PasswordResetEmail.Create(
            user.Email!,
            user.FirstName,
            BuildAccountUrl(PasswordResetPath, user.Id, code),
            BuildSiteUrl(),
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
        var message = LoginCodeEmail.Create(
            user.Email!,
            user.FirstName,
            code,
            BuildSiteUrl(),
            twoFactor.ChallengeLifetime
        );
        return emailSender.SendAsync(message, ct);
    }

    private string BuildSiteUrl()
    {
        return application.BaseUrl.TrimEnd('/');
    }

    private string BuildAccountUrl(string path, Guid userId, string code)
    {
        return $"{BuildSiteUrl()}{path}?userId={userId}&code={Uri.EscapeDataString(code)}";
    }
}
