using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.Auth;

public sealed class AccountEmails(
    IEmailSender emailSender,
    AccountVerificationOptions verification,
    PasswordResetOptions passwordReset,
    ApplicationOptions application
)
{
    private const string VerificationPath = "/verify-account";
    private const string PasswordResetPath = "/reset-password";

    public Task SendVerificationEmailAsync(User user, string otpCode, CancellationToken ct)
    {
        var message = VerificationEmail.Create(
            user.Email!,
            user.FirstName,
            otpCode,
            BuildAccountUrl(VerificationPath, user.Id, otpCode),
            BuildSiteUrl(),
            verification.OtpLifetime
        );
        return emailSender.SendAsync(message, ct);
    }

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

    private string BuildSiteUrl()
    {
        return application.BaseUrl.TrimEnd('/');
    }

    private string BuildAccountUrl(string path, Guid userId, string code)
    {
        return $"{BuildSiteUrl()}{path}?userId={userId}&code={Uri.EscapeDataString(code)}";
    }
}
