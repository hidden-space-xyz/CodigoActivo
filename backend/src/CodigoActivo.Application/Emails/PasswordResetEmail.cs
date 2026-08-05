using System.Net;
using CodigoActivo.Application.Localization;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

public static class PasswordResetEmail
{
    public static EmailMessage Create(
        string toAddress,
        string toName,
        string resetUrl,
        TimeSpan lifetime
    )
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(lifetime.TotalMinutes));
        var encodedName = WebUtility.HtmlEncode(toName);
        var encodedUrl = WebUtility.HtmlEncode(resetUrl);

        var textBody = $"""
            {AppStrings.EmailsSharedGreeting(toName)}

            {AppStrings.EmailsPasswordResetIntroText}

            {resetUrl}

            {AppStrings.EmailsPasswordResetExpiryText(minutes)}

            {AppStrings.EmailsPasswordResetIgnoreNote}
            """;

        var htmlBody = $"""
            <div style="font-family: Arial, Helvetica, sans-serif; max-width: 520px; margin: 0 auto; color: #1f2937;">
              <h2 style="color: #111827;">{AppStrings.EmailsPasswordResetHeading}</h2>
              <p>{AppStrings.EmailsSharedGreeting(encodedName)}</p>
              <p>{AppStrings.EmailsPasswordResetIntroHtml}</p>
              <p style="text-align: center; margin: 24px 0;">
                <a href="{encodedUrl}" style="display: inline-block; padding: 12px 28px; background: #2563eb; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold;">{AppStrings.EmailsPasswordResetButtonLabel}</a>
              </p>
              <p>{AppStrings.EmailsPasswordResetExpiryHtml(minutes)}</p>
              <p style="color: #6b7280; font-size: 13px;">{AppStrings.EmailsSharedFallbackLinkNote}<br>{encodedUrl}</p>
              <p style="color: #6b7280; font-size: 13px;">{AppStrings.EmailsPasswordResetIgnoreNote}</p>
            </div>
            """;

        return new EmailMessage(
            EmailKind.PasswordReset,
            toAddress,
            toName,
            AppStrings.EmailsPasswordResetSubject,
            htmlBody,
            textBody
        );
    }
}
