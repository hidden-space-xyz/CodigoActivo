using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.BlockedData.Commands;

/// <summary>
/// Carries the request to purge the data history, the consultations and the holds kept long
/// enough.
/// </summary>
public sealed record PurgeDataHistoryCommand : ICommand<Result<int>>, IScheduledCommand;

/// <summary>
/// Executes the command that physically removes the data history whose retention has ended,
/// except the history about an account an active hold keeps, and the consultations of the blocked
/// data and the lifted holds kept <see cref="BlockedDataConsultation.RetentionYears"/>.
/// </summary>
/// <param name="history">Retention of the data history.</param>
/// <param name="consultations">Repository of the consultations.</param>
/// <param name="holds">Repository of the holds.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class PurgeDataHistoryCommandHandler(
    IDataHistoryRetention history,
    IBlockedDataConsultationRepository consultations,
    IBlockedDataHoldRepository holds,
    IClock clock
) : ICommandHandler<PurgeDataHistoryCommand, Result<int>>
{
    /// <summary>
    /// Handles the request to purge the data history, the consultations and the holds kept long
    /// enough.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result carries the number of entries, consultations and holds removed.
    /// </returns>
    public async Task<Result<int>> HandleAsync(
        PurgeDataHistoryCommand command,
        CancellationToken ct = default
    )
    {
        var now = clock.UtcNow;
        var held = await holds.ListHeldAccountIdsAsync(ct);
        var entries = await history.PurgeExpiredAsync(now, [.. held.Select(id => id.Value)], ct);
        var opened = await consultations.PurgeOpenedUpToAsync(
            BlockedDataConsultation.PurgeCutoff(now),
            ct
        );
        var lifted = await holds.PurgeLiftedUpToAsync(BlockedDataHold.PurgeCutoff(now), ct);
        return entries + opened + lifted;
    }
}
