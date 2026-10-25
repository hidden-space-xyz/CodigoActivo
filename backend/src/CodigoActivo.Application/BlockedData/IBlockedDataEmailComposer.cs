using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Domain.BlockedData;

namespace CodigoActivo.Application.BlockedData;

/// <summary>
/// Composes the notice that tells the association's legal contact a consultation of the blocked
/// data was opened.
/// </summary>
public interface IBlockedDataEmailComposer
{
    /// <summary>
    /// Composes the notice of an opened consultation.
    /// </summary>
    /// <param name="recipient">Legal contact of the association.</param>
    /// <param name="consultation">The consultation opened.</param>
    /// <returns>The message to send.</returns>
    public EmailMessage ConsultationOpened(
        EmailRecipient recipient,
        OpenedConsultation consultation
    );
}

/// <summary>
/// Consultation of the blocked data as its notice describes it.
/// </summary>
/// <param name="OfficerName">Name of the account that opened it.</param>
/// <param name="Authority">Who the data is made available to.</param>
/// <param name="Reference">Reference of the request.</param>
/// <param name="OpenedAt">When it was opened.</param>
public sealed record OpenedConsultation(
    string OfficerName,
    BlockedDataAuthority Authority,
    string Reference,
    DateTimeOffset OpenedAt
);
