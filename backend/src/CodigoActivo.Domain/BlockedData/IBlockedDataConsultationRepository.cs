using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.BlockedData;

/// <summary>
/// Stores the consultations of the blocked data.
/// </summary>
public interface IBlockedDataConsultationRepository : IRepository<BlockedDataConsultation>
{
    /// <summary>
    /// Loads a consultation with what it recorded.
    /// </summary>
    /// <param name="id">Identifier of the consultation.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the consultation, or <see langword="null"/> when missing.</returns>
    public Task<BlockedDataConsultation?> GetByIdAsync(
        BlockedDataConsultationId id,
        CancellationToken ct = default
    );

    /// <summary>
    /// Locks a consultation until the open transaction ends and then loads it with what it
    /// recorded, so two searches in it never record the same account twice.
    /// </summary>
    /// <param name="id">Identifier of the consultation.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the consultation, or <see langword="null"/> when missing.</returns>
    public Task<BlockedDataConsultation?> GetLockedAsync(
        BlockedDataConsultationId id,
        CancellationToken ct = default
    );

    /// <summary>
    /// Physically removes the consultations opened up to a moment. It runs immediately, without
    /// waiting for the unit of work.
    /// </summary>
    /// <param name="openedUpTo">Latest opening moment whose consultation is removed.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of consultations removed.</returns>
    public Task<int> PurgeOpenedUpToAsync(
        DateTimeOffset openedUpTo,
        CancellationToken ct = default
    );

    /// <summary>
    /// Loads the consultations the association's legal contact has not been told about yet, oldest
    /// first, without what they recorded, to record the notice.
    /// </summary>
    /// <param name="limit">Most consultations to load.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the consultations.</returns>
    public Task<IReadOnlyList<BlockedDataConsultation>> ListNoticesPendingAsync(
        int limit,
        CancellationToken ct = default
    );
}
