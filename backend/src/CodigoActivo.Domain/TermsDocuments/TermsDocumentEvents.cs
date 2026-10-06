using CodigoActivo.Domain.Files;

namespace CodigoActivo.Domain.TermsDocuments;

/// <summary>
/// The name or content of a terms document was replaced.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the document.</param>
/// <param name="ReleasedFileIds">Identifiers of the files the previous content referenced and the new one does not.</param>
public sealed record TermsDocumentRewritten(
    TermsDocumentId TermsDocumentId,
    IReadOnlyCollection<StoredFileId> ReleasedFileIds
) : IReleasesFiles;

/// <summary>
/// A terms document was deleted.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the document.</param>
/// <param name="ReleasedFileIds">Identifiers of the files it referenced.</param>
public sealed record TermsDocumentDeleted(
    TermsDocumentId TermsDocumentId,
    IReadOnlyCollection<StoredFileId> ReleasedFileIds
) : IReleasesFiles;
