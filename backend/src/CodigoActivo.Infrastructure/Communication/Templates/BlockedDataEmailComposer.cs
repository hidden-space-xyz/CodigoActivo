using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.BlockedData;
using CodigoActivo.Application.Common;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Renders the notice of opened consultations of the blocked data with the site layout.
/// </summary>
/// <param name="application">Options that carry the public base URL of the site.</param>
/// <param name="clock">Clock whose time zone dates the consultations.</param>
public sealed class BlockedDataEmailComposer(ApplicationOptions application, IClock clock)
    : IBlockedDataEmailComposer
{
    /// <inheritdoc />
    public EmailMessage ConsultationsOpened(
        EmailRecipient recipient,
        IReadOnlyList<OpenedConsultation> consultations
    )
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return BlockedDataConsultationEmail.Create(
            recipient.Address,
            recipient.Name,
            consultations,
            clock.TimeZone,
            application.BaseUrl.TrimEnd('/')
        );
    }
}
