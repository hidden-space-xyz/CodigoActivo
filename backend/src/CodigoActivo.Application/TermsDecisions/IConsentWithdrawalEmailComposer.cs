using CodigoActivo.Application.Abstractions.Email;

namespace CodigoActivo.Application.TermsDecisions;

/// <summary>
/// Composes the notice that tells the association a consent was withdrawn.
/// </summary>
public interface IConsentWithdrawalEmailComposer
{
    /// <summary>
    /// Composes the notice for the association's legal contact.
    /// </summary>
    /// <param name="recipient">Legal contact of the association.</param>
    /// <param name="withdrawal">What was withdrawn, for whom and by whom.</param>
    /// <returns>The composed message.</returns>
    public EmailMessage Withdrawn(EmailRecipient recipient, ConsentWithdrawal withdrawal);
}

/// <summary>
/// A consent withdrawn, as the association is told about it.
/// </summary>
/// <param name="EventTitle">Title of the event.</param>
/// <param name="DocumentName">Name of the terms document that held the consent.</param>
/// <param name="PersonName">Full name of the person the consent was about.</param>
/// <param name="RecordedByName">Full name of the account that recorded the withdrawal.</param>
/// <param name="WithdrawnAt">When it was withdrawn.</param>
public sealed record ConsentWithdrawal(
    string EventTitle,
    string DocumentName,
    string PersonName,
    string RecordedByName,
    DateTimeOffset WithdrawnAt
);
