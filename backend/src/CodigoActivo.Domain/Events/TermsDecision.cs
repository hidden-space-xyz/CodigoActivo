using CodigoActivo.Domain.TermsDocuments;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Decision a person sends about one terms document when signing up.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="Accepted">Whether the person accepts it.</param>
/// <param name="Signature">PNG image of the signature drawn to accept it, for a document that asks for one; empty otherwise.</param>
public sealed record TermsDecision(
    TermsDocumentId TermsDocumentId,
    bool Accepted,
    ReadOnlyMemory<byte> Signature = default
);
