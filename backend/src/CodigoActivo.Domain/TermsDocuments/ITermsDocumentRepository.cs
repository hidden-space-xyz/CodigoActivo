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
    public Task<TermsDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Tells whether another document already uses a name.
    /// </summary>
    /// <param name="name">Name to look for, already trimmed.</param>
    /// <param name="exceptId">Document to ignore, when renaming it.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the name is taken.</returns>
    public Task<bool> NameExistsAsync(
        string name,
        Guid? exceptId = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Counts how many of the given documents exist.
    /// </summary>
    /// <param name="ids">Distinct identifiers to look for.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of existing documents.</returns>
    public Task<int> CountExistingAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    );
}
