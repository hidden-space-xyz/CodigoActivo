using System.Globalization;
using System.Net;
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
        var minutes = Math.Max(1, (int)Math.Ceiling(lifetime.TotalMinutes))
            .ToString(CultureInfo.InvariantCulture);
        var encodedName = WebUtility.HtmlEncode(toName);
        var encodedUrl = WebUtility.HtmlEncode(resetUrl);

        const string Subject = "Restablece tu contraseña de Código Activo";

        var textBody = $"""
            Hola {toName}:

            Hemos recibido una solicitud para restablecer la contraseña de tu cuenta de Código Activo. Puedes hacerlo desde este enlace:

            {resetUrl}

            El enlace caduca en {minutes} minutos.

            Si no has solicitado este cambio, puedes ignorar este mensaje; tu contraseña seguirá siendo la misma.
            """;

        var htmlBody = $"""
            <div style="font-family: Arial, Helvetica, sans-serif; max-width: 520px; margin: 0 auto; color: #1f2937;">
              <h2 style="color: #111827;">Restablece tu contraseña</h2>
              <p>Hola {encodedName}:</p>
              <p>Hemos recibido una solicitud para restablecer la contraseña de tu cuenta de <b>Código Activo</b>. Para elegir una nueva contraseña, pulsa el botón:</p>
              <p style="text-align: center; margin: 24px 0;">
                <a href="{encodedUrl}" style="display: inline-block; padding: 12px 28px; background: #2563eb; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold;">Restablecer mi contraseña</a>
              </p>
              <p>El enlace caduca en <b>{minutes} minutos</b>.</p>
              <p style="color: #6b7280; font-size: 13px;">Si el botón no funciona, copia esta dirección en tu navegador:<br>{encodedUrl}</p>
              <p style="color: #6b7280; font-size: 13px;">Si no has solicitado este cambio, puedes ignorar este mensaje; tu contraseña seguirá siendo la misma.</p>
            </div>
            """;

        return new EmailMessage(toAddress, toName, Subject, htmlBody, textBody);
    }
}
