namespace CodigoActivo.Domain.Users;

/// <summary>
/// Decision on a terms document of an event, as the legal copy keeps it.
/// </summary>
/// <param name="DocumentId">Identifier of the document.</param>
/// <param name="DocumentName">Name of the document.</param>
/// <param name="DocumentVersion">Version of the document the decision was last taken on.</param>
/// <param name="Accepted">Whether the document was accepted when the account was erased.</param>
/// <param name="DecidedAt">When it was last decided.</param>
/// <param name="PersonId">Identifier of the person the decision is about.</param>
/// <param name="DecidedBy">Identifier of the user who last decided: the person or their guardian.</param>
/// <param name="Signature">PNG image of the signature drawn to accept it, or <see langword="null"/> when none was asked for or it is no longer accepted.</param>
/// <param name="Changes">Every acceptance, decline and withdrawal of the decision, oldest first.</param>
public sealed record LegalCopyDecision(
    Guid DocumentId,
    string DocumentName,
    int DocumentVersion,
    bool Accepted,
    DateTimeOffset DecidedAt,
    Guid PersonId,
    Guid DecidedBy,
    ReadOnlyMemory<byte>? Signature,
    IReadOnlyList<LegalCopyDecisionChange> Changes
);

/// <summary>
/// One change of a decision on a terms document of an event, as the legal copy keeps it.
/// </summary>
/// <param name="Accepted">Whether the document was accepted; false for a decline or a withdrawal.</param>
/// <param name="DocumentVersion">Version of the document the change was made on.</param>
/// <param name="ChangedAt">When it changed.</param>
/// <param name="DecidedBy">Identifier of the user who decided: the person or their guardian.</param>
/// <param name="RecordedBy">Identifier of the account that recorded the change.</param>
/// <param name="Signature">PNG image of the signature drawn to accept it, or <see langword="null"/> when none was asked for.</param>
public sealed record LegalCopyDecisionChange(
    bool Accepted,
    int DocumentVersion,
    DateTimeOffset ChangedAt,
    Guid DecidedBy,
    Guid RecordedBy,
    ReadOnlyMemory<byte>? Signature
);
