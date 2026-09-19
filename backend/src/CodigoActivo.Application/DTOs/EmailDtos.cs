namespace CodigoActivo.Application.DTOs;

/// <summary>
/// Contains the client-supplied data used to send email.
/// </summary>
/// <param name="Subject">The subject value.</param>
/// <param name="Body">The body value.</param>
public record SendEmailRequest(string Subject, string Body)
{
    /// <summary>
    /// Identifies the subject max length configuration or policy value.
    /// </summary>
    public const int SubjectMaxLength = 200;

    /// <summary>
    /// Identifies the body max length configuration or policy value.
    /// </summary>
    public const int BodyMaxLength = 10000;
}

/// <summary>
/// Contains the send email data returned by the API. The email is delivered in the background, so
/// the response reports how many messages were accepted, not how many reached their mailbox.
/// </summary>
/// <param name="Queued">The queued value.</param>
/// <param name="Skipped">The skipped value.</param>
public record SendEmailResultResponse(int Queued, int Skipped);
