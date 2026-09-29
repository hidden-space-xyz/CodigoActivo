namespace CodigoActivo.Domain.Events;

/// <summary>
/// Decision a person sends about one terms document when signing up.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="Accepted">Whether the person accepts it.</param>
public sealed record TermsDecision(Guid TermsDocumentId, bool Accepted);
