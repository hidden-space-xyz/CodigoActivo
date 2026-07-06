using System.Globalization;
using System.Net;
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
        var minutes = Math.Max(1, (int)Math.Ceiling(lifetime.TotalMinutes))
            .ToString(CultureInfo.InvariantCulture);
        var encodedName = WebUtility.HtmlEncode(toName);
        var encodedUrl = WebUtility.HtmlEncode(verificationUrl);

        // The code stays out of the subject: subjects surface in lock-screen/notification previews
        // and relay logs, so keeping the OTP only in the body reduces its exposure.
        const string subject = "Verifica tu cuenta de Código Activo";

        var textBody = $"""
            Hola {toName}:

            Gracias por registrarte en Código Activo. Para activar tu cuenta puedes abrir este enlace:

            {verificationUrl}

            O, si lo prefieres, copia este código en la página de registro:

            {code}

            El enlace y el código caducan en {minutes} minutos.

            Si no has creado una cuenta en Código Activo, puedes ignorar este mensaje.
            """;

        var htmlBody = $"""
            <div style="font-family: Arial, Helvetica, sans-serif; max-width: 520px; margin: 0 auto; color: #1f2937;">
              <h2 style="color: #111827;">Verifica tu cuenta</h2>
              <p>Hola {encodedName}:</p>
              <p>Gracias por registrarte en <b>Código Activo</b>. Para activar tu cuenta, pulsa el botón:</p>
              <p style="text-align: center; margin: 24px 0;">
                <a href="{encodedUrl}" style="display: inline-block; padding: 12px 28px; background: #2563eb; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold;">Verificar mi cuenta</a>
              </p>
              <p>O, si lo prefieres, copia este código en la página de registro:</p>
              <p style="font-family: 'Courier New', monospace; font-size: 18px; font-weight: bold; text-align: center; padding: 16px; background: #f3f4f6; border-radius: 8px; word-break: break-all;">{code}</p>
              <p>El enlace y el código caducan en <b>{minutes} minutos</b>.</p>
              <p style="color: #6b7280; font-size: 13px;">Si el botón no funciona, copia esta dirección en tu navegador:<br>{encodedUrl}</p>
              <p style="color: #6b7280; font-size: 13px;">Si no has creado una cuenta en Código Activo, puedes ignorar este mensaje.</p>
            </div>
            """;

        return new EmailMessage(toAddress, toName, subject, htmlBody, textBody);
    }
}
