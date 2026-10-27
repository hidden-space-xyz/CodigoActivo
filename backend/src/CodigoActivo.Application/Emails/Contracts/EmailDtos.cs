namespace CodigoActivo.Application.Emails.Contracts;

/// <summary>
/// Contains the send email data returned by the API. The email is delivered in the background, so
/// the response reports how many messages were accepted, not how many reached their mailbox.
/// </summary>
/// <param name="Queued">Messages accepted for delivery.</param>
/// <param name="Skipped">Matching users left out because they have no email address or have not confirmed it.</param>
public record SendEmailResultResponse(int Queued, int Skipped);

/// <summary>
/// Describes who a manual email would reach, selected exactly as the send endpoints do.
/// </summary>
/// <param name="Recipients">
/// How many distinct confirmed email addresses a notice would be sent to; a news email leaves out
/// the <c>WithoutConsent</c> ones.
/// </param>
/// <param name="WithoutConsent">
/// How many of those recipients have not agreed to receive news by email.
/// </param>
public record EmailAudienceResponse(int Recipients, int WithoutConsent);
