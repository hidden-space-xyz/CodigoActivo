using System.Net;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

public static class ActivitySignupDecisionEmail
{
    private const string ConfirmedHeading = "Inscripción confirmada";

    private const string ConfirmedNote =
        "Si finalmente no fuera posible asistir, te agradecemos que la anules con la mayor antelación posible "
        + "desde tu área personal, para que otra persona pueda ocupar la plaza.";

    private const string DeniedHeading = "Inscripción rechazada";

    public static EmailMessage Confirmed(
        string toAddress,
        string toName,
        string? participantName,
        string? roleName,
        ActivityEmailDetails details,
        TimeZoneInfo timeZone
    )
    {
        return Create(
            toAddress,
            toName,
            details,
            timeZone,
            new DecisionContent(
                ConfirmedHeading,
                $"Inscripción confirmada: {details.ActivityTitle}",
                ConfirmedIntro(participantName),
                ConfirmedIntro(WebUtility.HtmlEncode(participantName)),
                ConfirmedNote,
                "Ver el evento",
                roleName
            )
        );
    }

    public static EmailMessage Denied(
        string toAddress,
        string toName,
        string? participantName,
        ActivityEmailDetails details,
        TimeZoneInfo timeZone
    )
    {
        return Create(
            toAddress,
            toName,
            details,
            timeZone,
            new DecisionContent(
                DeniedHeading,
                $"Inscripción rechazada: {details.ActivityTitle}",
                DeniedIntro(participantName),
                DeniedIntro(WebUtility.HtmlEncode(participantName)),
                null,
                "Ver otras actividades",
                null
            )
        );
    }

    private static EmailMessage Create(
        string toAddress,
        string toName,
        ActivityEmailDetails details,
        TimeZoneInfo timeZone,
        DecisionContent content
    )
    {
        var encodedName = WebUtility.HtmlEncode(toName);
        var encodedUrl = WebUtility.HtmlEncode(details.EventUrl);
        var noteText = content.Note is null ? string.Empty : $"{content.Note}\n\n";
        var noteHtml = content.Note is null ? string.Empty : $"<p>{content.Note}</p>";

        var textBody = $"""
            Hola {toName}:

            {content.IntroText}

            {details.ToTextBlock(timeZone, content.RoleName)}

            {noteText}{details.EventUrl}

            Un saludo,
            Equipo de Código Activo
            """;

        var htmlBody = $"""
            <div style="font-family: Arial, Helvetica, sans-serif; max-width: 520px; margin: 0 auto; color: #1f2937;">
              <h2 style="color: #111827;">{content.Heading}</h2>
              <p>Hola {encodedName}:</p>
              <p>{content.IntroHtml}</p>
              {details.ToHtmlBlock(timeZone, content.RoleName)}
              {noteHtml}
              <p style="text-align: center; margin: 24px 0;">
                <a href="{encodedUrl}" style="display: inline-block; padding: 12px 28px; background: #2563eb; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold;">{content.ButtonLabel}</a>
              </p>
              <p style="color: #6b7280; font-size: 13px;">Si el botón no funciona, copia esta dirección en tu navegador:<br>{encodedUrl}</p>
              <p style="color: #6b7280; font-size: 13px;">Un saludo,<br>Equipo de Código Activo</p>
            </div>
            """;

        return new EmailMessage(
            EmailKind.ActivityNotification,
            toAddress,
            toName,
            content.Subject,
            htmlBody,
            textBody
        );
    }

    private static string ConfirmedIntro(string? participantName)
    {
        return $"Un miembro de la asociación ha revisado {SignupPhrase(participantName)} en esta actividad "
            + "y la ha aprobado. La plaza queda reservada.";
    }

    private static string DeniedIntro(string? participantName)
    {
        return $"Un miembro de la asociación ha revisado {SignupPhrase(participantName)} en esta actividad "
            + "y la ha rechazado. Sentimos no poder confirmar la plaza en esta ocasión.";
    }

    private static string SignupPhrase(string? participantName)
    {
        return string.IsNullOrWhiteSpace(participantName)
            ? "tu inscripción"
            : $"la inscripción de {participantName}";
    }

    private sealed record DecisionContent(
        string Heading,
        string Subject,
        string IntroText,
        string IntroHtml,
        string? Note,
        string ButtonLabel,
        string? RoleName
    );
}
