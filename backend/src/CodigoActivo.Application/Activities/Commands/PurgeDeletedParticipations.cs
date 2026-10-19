using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the request to purge the blocked copies of removed participation kept long enough.
/// </summary>
public sealed record PurgeDeletedParticipationsCommand : ICommand<Result<int>>, IScheduledCommand;

/// <summary>
/// Executes the command that physically removes the blocked copies of removed participation
/// whose retention period, set by <see cref="DeletedParticipation.RetentionYears"/>, has passed.
/// </summary>
/// <param name="deletedParticipations">Repository of the blocked copies.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class PurgeDeletedParticipationsCommandHandler(
    IDeletedParticipationRepository deletedParticipations,
    IClock clock
) : ICommandHandler<PurgeDeletedParticipationsCommand, Result<int>>
{
    /// <summary>
    /// Handles the request to purge the blocked copies kept long enough.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result carries the number of copies removed.</returns>
    public async Task<Result<int>> HandleAsync(
        PurgeDeletedParticipationsCommand command,
        CancellationToken ct = default
    )
    {
        return await deletedParticipations.PurgeDeletedUpToAsync(
            DeletedParticipation.PurgeCutoff(clock.UtcNow),
            ct
        );
    }
}
