using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.TermsDecisions;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Renders the notice of a withdrawn consent with the site layout.
/// </summary>
/// <param name="application">Options that carry the public base URL of the site.</param>
/// <param name="clock">Clock whose time zone dates the withdrawal.</param>
public sealed class ConsentWithdrawalEmailComposer(ApplicationOptions application, IClock clock)
    : IConsentWithdrawalEmailComposer
{
    /// <inheritdoc />
    public EmailMessage Withdrawn(EmailRecipient recipient, ConsentWithdrawal withdrawal)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return ConsentWithdrawalEmail.Create(
            recipient.Address,
            recipient.Name,
            withdrawal,
            clock.TimeZone,
            application.BaseUrl.TrimEnd('/')
        );
    }
}
