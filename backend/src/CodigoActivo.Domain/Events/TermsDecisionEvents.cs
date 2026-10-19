using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// A consent given about a terms document of an event for a person was withdrawn.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="UserId">Identifier of the person the consent was about.</param>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="RecordedById">Identifier of the account that recorded the withdrawal.</param>
public sealed record TermsConsentWithdrawn(
    EventId EventId,
    UserId UserId,
    TermsDocumentId TermsDocumentId,
    UserId RecordedById
) : IDomainEvent;
