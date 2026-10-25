using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.BlockedData;
using CodigoActivo.Application.Common;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Renders the notice of an opened consultation of the blocked data with the site layout.
/// </summary>
/// <param name="application">Options that carry the public base URL of the site.</param>
/// <param name="clock">Clock whose time zone dates the consultation.</param>
public sealed class BlockedDataEmailComposer(ApplicationOptions application, IClock clock)
    : IBlockedDataEmailComposer
{
    /// <inheritdoc />
    public EmailMessage ConsultationOpened(
        EmailRecipient recipient,
        OpenedConsultation consultation
    )
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return BlockedDataConsultationEmail.Create(
            recipient.Address,
            recipient.Name,
            consultation,
            clock.TimeZone,
            application.BaseUrl.TrimEnd('/')
        );
    }
}
