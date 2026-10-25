using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.BlockedData.Commands;

/// <summary>
/// Carries the request to purge the data history and the consultations kept long enough.
/// </summary>
public sealed record PurgeDataHistoryCommand : ICommand<Result<int>>, IScheduledCommand;

/// <summary>
/// Executes the command that physically removes the data history whose retention has ended and
/// the consultations of the blocked data kept <see cref="BlockedDataConsultation.RetentionYears"/>.
/// </summary>
/// <param name="history">Retention of the data history.</param>
/// <param name="consultations">Repository of the consultations.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class PurgeDataHistoryCommandHandler(
    IDataHistoryRetention history,
    IBlockedDataConsultationRepository consultations,
    IClock clock
) : ICommandHandler<PurgeDataHistoryCommand, Result<int>>
{
    /// <summary>
    /// Handles the request to purge the data history and the consultations kept long enough.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result carries the number of entries and consultations removed.</returns>
    public async Task<Result<int>> HandleAsync(
        PurgeDataHistoryCommand command,
        CancellationToken ct = default
    )
    {
        var now = clock.UtcNow;
        var entries = await history.PurgeExpiredAsync(now, ct);
        var opened = await consultations.PurgeOpenedUpToAsync(
            BlockedDataConsultation.PurgeCutoff(now),
            ct
        );
        return entries + opened;
    }
}
