using System.Net;
using CodigoActivo.Application.Localization;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

public static class VerificationEmail
{
    public static EmailMessage Create(
        string toAddress,
        string toName,
        string code,
        string verificationUrl,
        TimeSpan lifetime
    )
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(lifetime.TotalMinutes));
        var encodedName = WebUtility.HtmlEncode(toName);
        var encodedUrl = WebUtility.HtmlEncode(verificationUrl);

        var textBody = $"""
            {AppStrings.EmailsSharedGreeting(toName)}

            {AppStrings.EmailsVerificationIntroText}

            {verificationUrl}

            {AppStrings.EmailsVerificationCodePrompt}

            {code}

            {AppStrings.EmailsVerificationExpiryText(minutes)}

            {AppStrings.EmailsVerificationIgnoreNote}
            """;

        var htmlBody = $"""
            <div style="font-family: Arial, Helvetica, sans-serif; max-width: 520px; margin: 0 auto; color: #1f2937;">
              <h2 style="color: #111827;">{AppStrings.EmailsVerificationHeading}</h2>
              <p>{AppStrings.EmailsSharedGreeting(encodedName)}</p>
              <p>{AppStrings.EmailsVerificationIntroHtml}</p>
              <p style="text-align: center; margin: 24px 0;">
                <a href="{encodedUrl}" style="display: inline-block; padding: 12px 28px; background: #2563eb; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold;">{AppStrings.EmailsVerificationButtonLabel}</a>
              </p>
              <p>{AppStrings.EmailsVerificationCodePrompt}</p>
              <p style="font-family: 'Courier New', monospace; font-size: 18px; font-weight: bold; text-align: center; padding: 16px; background: #f3f4f6; border-radius: 8px; word-break: break-all;">{code}</p>
              <p>{AppStrings.EmailsVerificationExpiryHtml(minutes)}</p>
              <p style="color: #6b7280; font-size: 13px;">{AppStrings.EmailsSharedFallbackLinkNote}<br>{encodedUrl}</p>
              <p style="color: #6b7280; font-size: 13px;">{AppStrings.EmailsVerificationIgnoreNote}</p>
            </div>
            """;

        return new EmailMessage(
            EmailKind.AccountVerification,
            toAddress,
            toName,
            AppStrings.EmailsVerificationSubject,
            htmlBody,
            textBody
        );
    }
}
