using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.TermsDecisions;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Renders the notices of withdrawn and erased consents with the site layout.
/// </summary>
/// <param name="application">Options that carry the public base URL of the site.</param>
/// <param name="clock">Clock whose time zone dates the withdrawals and the consents.</param>
public sealed class ConsentWithdrawalEmailComposer(ApplicationOptions application, IClock clock)
    : IConsentWithdrawalEmailComposer
{
    /// <inheritdoc />
    public EmailMessage Withdrawn(
        EmailRecipient recipient,
        IReadOnlyList<ConsentWithdrawal> withdrawals
    )
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return ConsentWithdrawalEmail.Create(
            recipient.Address,
            recipient.Name,
            withdrawals,
            clock.TimeZone,
            application.BaseUrl.TrimEnd('/')
        );
    }

    /// <inheritdoc />
    public EmailMessage Erased(EmailRecipient recipient, IReadOnlyList<ErasedConsent> consents)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return ErasedConsentEmail.Create(
            recipient.Address,
            recipient.Name,
            consents,
            clock.TimeZone,
            application.BaseUrl.TrimEnd('/')
        );
    }
}
