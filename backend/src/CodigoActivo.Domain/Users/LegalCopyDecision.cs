namespace CodigoActivo.Domain.Users;

/// <summary>
/// Decision on a terms document of an event, as the legal copy keeps it.
/// </summary>
/// <param name="DocumentId">Identifier of the document.</param>
/// <param name="DocumentName">Name of the document.</param>
/// <param name="Accepted">Whether the document was accepted.</param>
/// <param name="DecidedAt">When it was decided.</param>
/// <param name="DecidedBy">Identifier of the user who decided.</param>
/// <param name="Signature">PNG image of the signature drawn to accept it, or <see langword="null"/> when none was asked for.</param>
public sealed record LegalCopyDecision(
    Guid DocumentId,
    string DocumentName,
    bool Accepted,
    DateTimeOffset DecidedAt,
    Guid DecidedBy,
    ReadOnlyMemory<byte>? Signature
);
