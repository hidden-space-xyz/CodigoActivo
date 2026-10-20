using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Files;

/// <summary>
/// Stores and loads uploaded files, and tells which of them the content still references.
/// </summary>
public interface IStoredFileRepository : IRepository<StoredFile>
{
    /// <summary>
    /// Loads a file to change it.
    /// </summary>
    /// <param name="id">Identifier of the file.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the file, or <see langword="null"/> when it does not exist.</returns>
    public Task<StoredFile?> GetByIdAsync(StoredFileId id, CancellationToken ct = default);

    /// <summary>
    /// Loads the files that exist among the given ones.
    /// </summary>
    /// <param name="ids">Identifiers of the files.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the existing files.</returns>
    public Task<IReadOnlyList<StoredFile>> ListByIdsAsync(
        IReadOnlyCollection<StoredFileId> ids,
        CancellationToken ct = default
    );

    /// <summary>
    /// Lists the files uploaded at or before an instant, whether or not anything references them.
    /// </summary>
    /// <param name="cutoff">Latest upload instant to include.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifiers of the files.</returns>
    public Task<IReadOnlyList<StoredFileId>> ListIdsUploadedUpToAsync(
        DateTimeOffset cutoff,
        CancellationToken ct = default
    );

    /// <summary>
    /// Returns which of the given files exist and, inside a transaction, keeps them from being
    /// deleted until it ends (`FOR KEY SHARE`), so content that is about to reference them can be
    /// saved first.
    /// </summary>
    /// <param name="ids">Identifiers of the files.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifiers that exist.</returns>
    public Task<IReadOnlyList<StoredFileId>> LockExistingAsync(
        IReadOnlyCollection<StoredFileId> ids,
        CancellationToken ct = default
    );

    /// <summary>
    /// Locks the given files for deletion (`FOR UPDATE`) until the transaction ends, waiting for any
    /// save that is about to reference them, and returns which of them exist.
    /// </summary>
    /// <param name="ids">Identifiers of the files.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifiers that exist.</returns>
    public Task<IReadOnlyList<StoredFileId>> LockForDeletionAsync(
        IReadOnlyCollection<StoredFileId> ids,
        CancellationToken ct = default
    );

    /// <summary>
    /// Returns which of the given files any content still references.
    /// </summary>
    /// <param name="fileIds">Identifiers of the candidate files.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the referenced identifiers.</returns>
    public Task<IReadOnlyList<StoredFileId>> GetInUseAsync(
        IReadOnlyCollection<StoredFileId> fileIds,
        CancellationToken ct = default
    );
}
