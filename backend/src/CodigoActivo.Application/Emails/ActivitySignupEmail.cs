using System.Net;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

public sealed record ActivitySignupParticipant(string FullName, string RoleName);

public static class ActivitySignupEmail
{
    private const string Intro =
        "Hemos recibido tu solicitud de inscripción y ya ha quedado registrada.";

    private const string Pending =
        "La organización la revisará y te avisaremos por correo en cuanto la plaza quede confirmada. "
        + "Por ahora no necesitas hacer nada más.";

    public static EmailMessage Create(
        string toAddress,
        string toName,
        ActivityEmailDetails details,
        IReadOnlyList<ActivitySignupParticipant> participants,
        TimeZoneInfo timeZone,
        string accountUrl
    )
    {
        var subject = $"Inscripción recibida: {details.ActivityTitle}";
        var encodedName = WebUtility.HtmlEncode(toName);
        var encodedUrl = WebUtility.HtmlEncode(accountUrl);

        var participantsText = string.Join(
            "\n",
            participants.Select(participant =>
                $"- {participant.FullName} ({participant.RoleName})"
            )
        );

        var participantsHtml = string.Concat(
            participants.Select(participant =>
                $"<li style=\"margin-bottom: 4px;\">{WebUtility.HtmlEncode(participant.FullName)}"
                + $" — <b>{WebUtility.HtmlEncode(participant.RoleName)}</b></li>"
            )
        );

        var textBody = $"""
            Hola {toName}:

            {Intro}

            {details.ToTextBlock(timeZone)}

            Personas inscritas:
            {participantsText}

            {Pending}

            Puedes consultar el estado de tus inscripciones cuando quieras desde tu área personal:

            {accountUrl}

            Un saludo,
            Equipo de Código Activo
            """;

        var htmlBody = $"""
            <div style="font-family: Arial, Helvetica, sans-serif; max-width: 520px; margin: 0 auto; color: #1f2937;">
              <h2 style="color: #111827;">Inscripción recibida</h2>
              <p>Hola {encodedName}:</p>
              <p>{Intro}</p>
              {details.ToHtmlBlock(timeZone)}
              <p style="margin-bottom: 4px;">Personas inscritas:</p>
              <ul style="margin-top: 0; padding-left: 20px;">{participantsHtml}</ul>
              <p>{Pending}</p>
              <p style="text-align: center; margin: 24px 0;">
                <a href="{encodedUrl}" style="display: inline-block; padding: 12px 28px; background: #2563eb; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold;">Ver mis inscripciones</a>
              </p>
              <p style="color: #6b7280; font-size: 13px;">Si el botón no funciona, copia esta dirección en tu navegador:<br>{encodedUrl}</p>
              <p style="color: #6b7280; font-size: 13px;">Un saludo,<br>Equipo de Código Activo</p>
            </div>
            """;

        return new EmailMessage(
            EmailKind.ActivityNotification,
            toAddress,
            toName,
            subject,
            htmlBody,
            textBody
        );
    }
}
