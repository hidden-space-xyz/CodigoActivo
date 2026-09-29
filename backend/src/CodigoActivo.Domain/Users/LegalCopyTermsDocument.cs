namespace CodigoActivo.Domain.Users;

/// <summary>
/// Terms document decided by the household, with its text at the time of the erasure, since the
/// document may change or disappear later.
/// </summary>
/// <param name="Id">Identifier of the document.</param>
/// <param name="Name">Name of the document.</param>
/// <param name="TextAtDeletion">Rich text of the document, as stored when the account was erased.</param>
public sealed record LegalCopyTermsDocument(Guid Id, string Name, string TextAtDeletion);
