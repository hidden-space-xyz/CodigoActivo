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
/// Contains the send email data returned by the API.
/// </summary>
/// <param name="Sent">The sent value.</param>
/// <param name="Skipped">The skipped value.</param>
/// <param name="Failed">The failed value.</param>
public record SendEmailResultResponse(int Sent, int Skipped, int Failed);
