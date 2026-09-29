using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Files;

/// <summary>
/// Stores and loads uploaded files, and tells which of them the content still references.
/// </summary>
public interface IFileRepository : IRepository<FileEntity>
{
    /// <summary>
    /// Loads a file to change it.
    /// </summary>
    /// <param name="id">Identifier of the file.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the file, or <see langword="null"/> when it does not exist.</returns>
    public Task<FileEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Loads the files that exist among the given ones.
    /// </summary>
    /// <param name="ids">Identifiers of the files.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the existing files.</returns>
    public Task<IReadOnlyList<FileEntity>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    );

    /// <summary>
    /// Tells whether a file exists.
    /// </summary>
    /// <param name="id">Identifier of the file.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the file exists.</returns>
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Tells whether any content uses the file as thumbnail or embeds it in rich text.
    /// </summary>
    /// <param name="fileId">Identifier of the file.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the file is referenced.</returns>
    public Task<bool> IsInUseAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// Returns which of the given files any content still references.
    /// </summary>
    /// <param name="fileIds">Identifiers of the candidate files.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the referenced identifiers.</returns>
    public Task<IReadOnlyList<Guid>> GetInUseAsync(
        IReadOnlyCollection<Guid> fileIds,
        CancellationToken ct = default
    );
}
