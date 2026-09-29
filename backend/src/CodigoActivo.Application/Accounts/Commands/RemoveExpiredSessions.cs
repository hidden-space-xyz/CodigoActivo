using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the request to remove the sessions that are no longer accepted.
/// </summary>
public sealed record RemoveExpiredSessionsCommand : ICommand<int>;

/// <summary>
/// Executes the command that removes the sessions past their expiry.
/// </summary>
/// <param name="sessions">Repository used to persist and retrieve sessions.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class RemoveExpiredSessionsCommandHandler(
    IUserSessionRepository sessions,
    IClock clock
) : ICommandHandler<RemoveExpiredSessionsCommand, int>
{
    /// <summary>
    /// Handles the request to remove the sessions that are no longer accepted.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of sessions removed.</returns>
    public Task<int> HandleAsync(
        RemoveExpiredSessionsCommand command,
        CancellationToken ct = default
    )
    {
        return sessions.RemoveExpiredAsync(clock.UtcNow, ct: ct);
    }
}
