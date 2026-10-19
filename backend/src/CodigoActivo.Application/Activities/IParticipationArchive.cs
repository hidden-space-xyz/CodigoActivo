using CodigoActivo.Domain.Activities;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Storage steps that keep the blocked copy of the participation a removal takes away. They run
/// inside the transaction of <see cref="ParticipationEraser"/>.
/// </summary>
public interface IParticipationArchive
{
    /// <summary>
    /// Locks the rows the removal takes away and those it would take away if they were added now,
    /// so nothing changes them until the removal commits.
    /// </summary>
    /// <param name="deletion">What is removed.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that completes once the rows are locked.</returns>
    public Task LockAsync(ParticipationDeletion deletion, CancellationToken ct = default);

    /// <summary>
    /// Builds the blocked copy of the participation the removal takes away, as it stands now.
    /// </summary>
    /// <param name="deletion">What is removed, by whom and when.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is the JSON document of the copy, or <see langword="null"/> when the
    /// removal takes away no signup or terms decision.
    /// </returns>
    public Task<string?> CaptureCopyAsync(
        ParticipationDeletion deletion,
        CancellationToken ct = default
    );
}
