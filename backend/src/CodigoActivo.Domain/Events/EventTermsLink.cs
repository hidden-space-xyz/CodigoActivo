using CodigoActivo.Domain.TermsDocuments;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Terms document to link to an event.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="Required">Whether accepting it is required to sign up.</param>
public sealed record EventTermsLink(TermsDocumentId TermsDocumentId, bool Required);
