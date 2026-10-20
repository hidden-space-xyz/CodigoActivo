namespace CodigoActivo.Domain.Users.Erasure;

/// <summary>
/// Terms document decided by the household, with its text at the time of the erasure, since the
/// document may change or disappear later, and the earlier versions the decisions it keeps were
/// taken on.
/// </summary>
/// <param name="Id">Identifier of the document.</param>
/// <param name="Name">Name of the document.</param>
/// <param name="Version">Number of the version current when the account was erased.</param>
/// <param name="TextAtDeletion">Rich text of the document, as stored when the account was erased.</param>
/// <param name="PreviousVersions">Earlier versions some kept decision or change was taken on.</param>
public sealed record LegalCopyTermsDocument(
    Guid Id,
    string Name,
    int Version,
    string TextAtDeletion,
    IReadOnlyList<LegalCopyTermsDocumentVersion> PreviousVersions
);

/// <summary>
/// Earlier version of a terms document, as the legal copy keeps it.
/// </summary>
/// <param name="Number">Number of the version.</param>
/// <param name="Name">Name the document had in this version.</param>
/// <param name="Text">Rich text of this version.</param>
public sealed record LegalCopyTermsDocumentVersion(int Number, string Name, string Text);
