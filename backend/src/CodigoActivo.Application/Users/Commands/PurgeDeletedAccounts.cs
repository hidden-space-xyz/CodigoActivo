using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the request to purge the legal copies kept long enough.
/// </summary>
public sealed record PurgeDeletedAccountsCommand : ICommand<int>;

/// <summary>
/// Executes the command that physically removes the legal copies whose retention period, set by
/// <see cref="DeletedAccount.RetentionYears"/>, has passed.
/// </summary>
/// <param name="deletedAccounts">Repository of the legal copies.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class PurgeDeletedAccountsCommandHandler(
    IDeletedAccountRepository deletedAccounts,
    IClock clock
) : ICommandHandler<PurgeDeletedAccountsCommand, int>
{
    /// <summary>
    /// Handles the request to purge the legal copies kept long enough.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of copies removed.</returns>
    public Task<int> HandleAsync(
        PurgeDeletedAccountsCommand command,
        CancellationToken ct = default
    )
    {
        return deletedAccounts.RemoveDeletedUpToAsync(DeletedAccount.PurgeCutoff(clock.UtcNow), ct);
    }
}
