using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Domain.BlockedData;

namespace CodigoActivo.Application.BlockedData;

/// <summary>
/// Composes the notice that tells the association's legal contact consultations of the blocked
/// data were opened.
/// </summary>
public interface IBlockedDataEmailComposer
{
    /// <summary>
    /// Composes one notice that lists consultations the legal contact was not told about yet.
    /// </summary>
    /// <param name="recipient">Legal contact of the association.</param>
    /// <param name="consultations">The consultations opened; at least one.</param>
    /// <returns>The message to send.</returns>
    public EmailMessage ConsultationsOpened(
        EmailRecipient recipient,
        IReadOnlyList<OpenedConsultation> consultations
    );
}

/// <summary>
/// Consultation of the blocked data as its notice describes it.
/// </summary>
/// <param name="OfficerName">Name of the account that opened it.</param>
/// <param name="Requester">Who asks for the data.</param>
/// <param name="Reference">Reference of the request.</param>
/// <param name="OpenedAt">When it was opened.</param>
public sealed record OpenedConsultation(
    string OfficerName,
    string Requester,
    string Reference,
    DateTimeOffset OpenedAt
);
