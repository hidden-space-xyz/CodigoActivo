using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.TermsDocuments;

/// <summary>
/// Stores and loads terms documents.
/// </summary>
public interface ITermsDocumentRepository : IRepository<TermsDocument>
{
    /// <summary>
    /// Loads a terms document to change it.
    /// </summary>
    /// <param name="id">Identifier of the document.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the document, or <see langword="null"/> when it does not exist.</returns>
    public Task<TermsDocument?> GetByIdAsync(TermsDocumentId id, CancellationToken ct = default);

    /// <summary>
    /// Locks the stored document until the running transaction ends and reloads it, so rewrites of
    /// the same document run one after another and each one keeps, as a previous version, the text
    /// the one before it stored. Changes staged on the document before the call are discarded.
    /// </summary>
    /// <param name="termsDocument">Document loaded in the unit of work.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="false"/> when the document no longer exists.</returns>
    public Task<bool> LockAsync(TermsDocument termsDocument, CancellationToken ct = default);

    /// <summary>
    /// Tells whether another document already uses a name, ignoring letter case.
    /// </summary>
    /// <param name="name">Name to look for, already trimmed.</param>
    /// <param name="exceptId">Document to ignore, when renaming it.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the name is taken.</returns>
    public Task<bool> NameExistsAsync(
        string name,
        TermsDocumentId? exceptId = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Counts how many of the given documents exist.
    /// </summary>
    /// <param name="ids">Distinct identifiers to look for.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of existing documents.</returns>
    public Task<int> CountExistingAsync(
        IReadOnlyCollection<TermsDocumentId> ids,
        CancellationToken ct = default
    );

    /// <summary>
    /// Lists which of the given documents ask for a handwritten signature to be accepted.
    /// </summary>
    /// <param name="ids">Identifiers of the documents to look at.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifiers of the documents that ask for one.</returns>
    public Task<IReadOnlyCollection<TermsDocumentId>> ListRequiringSignatureAsync(
        IReadOnlyCollection<TermsDocumentId> ids,
        CancellationToken ct = default
    );

    /// <summary>
    /// Lists which of the given documents hold a consent.
    /// </summary>
    /// <param name="ids">Identifiers of the documents to look at.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifiers of the consents.</returns>
    public Task<IReadOnlyCollection<TermsDocumentId>> ListConsentsAsync(
        IReadOnlyCollection<TermsDocumentId> ids,
        CancellationToken ct = default
    );

    /// <summary>
    /// Reads the current version of each of the given documents.
    /// </summary>
    /// <param name="ids">Identifiers of the documents to look at.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result maps each document found to its current version.</returns>
    public Task<IReadOnlyDictionary<TermsDocumentId, int>> ListVersionsAsync(
        IReadOnlyCollection<TermsDocumentId> ids,
        CancellationToken ct = default
    );
}
