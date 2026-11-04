using CodigoActivo.Application.Abstractions.Email;

namespace CodigoActivo.Application.TermsDecisions;

/// <summary>
/// Composes the notices that tell the association consents were withdrawn or erased.
/// </summary>
public interface IConsentWithdrawalEmailComposer
{
    /// <summary>
    /// Composes one notice for the association's legal contact with every withdrawal it was not
    /// told about yet.
    /// </summary>
    /// <param name="recipient">Legal contact of the association.</param>
    /// <param name="withdrawals">What was withdrawn, for whom and by whom; at least one.</param>
    /// <returns>The composed message.</returns>
    public EmailMessage Withdrawn(
        EmailRecipient recipient,
        IReadOnlyList<ConsentWithdrawal> withdrawals
    );

    /// <summary>
    /// Composes one notice for the association's legal contact with the consents given that an
    /// erasure deletes, which can no longer be consulted to cover what they allowed.
    /// </summary>
    /// <param name="recipient">Legal contact of the association.</param>
    /// <param name="consents">What was consented, for whom and by whom; at least one.</param>
    /// <returns>The composed message.</returns>
    public EmailMessage Erased(EmailRecipient recipient, IReadOnlyList<ErasedConsent> consents);
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

/// <summary>
/// A consent given that an erasure deletes, as the association is told about it.
/// </summary>
/// <param name="EventTitle">Title of the event.</param>
/// <param name="DocumentName">Name of the terms document that held the consent.</param>
/// <param name="PersonName">Full name of the person the consent was about.</param>
/// <param name="DecidedByName">Full name of whoever gave it: the person or their guardian.</param>
/// <param name="GivenAt">When it was given.</param>
public sealed record ErasedConsent(
    string EventTitle,
    string DocumentName,
    string PersonName,
    string DecidedByName,
    DateTimeOffset GivenAt
);
