namespace CodigoActivo.Application.Emails.Contracts;

/// <summary>
/// Contains the send email data returned by the API. The email is delivered in the background, so
/// the response reports how many messages were accepted, not how many reached their mailbox.
/// </summary>
/// <param name="Queued">The queued value.</param>
/// <param name="Skipped">The skipped value.</param>
public record SendEmailResultResponse(int Queued, int Skipped);

/// <summary>
/// Describes who a manual email would reach, selected exactly as the send endpoints do.
/// </summary>
/// <param name="Recipients">Distinct email addresses the message would be sent to.</param>
/// <param name="WithoutConsent">
/// How many of those recipients have not agreed to receive promotional content.
/// </param>
public record EmailAudienceResponse(int Recipients, int WithoutConsent);
