using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Erasure;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the request to erase the accounts nobody verified in time.
/// </summary>
public sealed record PurgeUnverifiedAccountsCommand : ICommand<Result<int>>, IScheduledCommand;

/// <summary>
/// Executes the command that erases, with their dependents, the accounts still
/// unverified <see cref="User.UnverifiedRetentionDays"/> after their registration, each one in its
/// own transaction.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="eraser">Eraser that deletes an account and keeps its data history.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class PurgeUnverifiedAccountsCommandHandler(
    IUserRepository users,
    AccountEraser eraser,
    IClock clock
) : ICommandHandler<PurgeUnverifiedAccountsCommand, Result<int>>
{
    /// <summary>
    /// Handles the request to erase the accounts nobody verified in time.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result carries the number of accounts erased.</returns>
    public async Task<Result<int>> HandleAsync(
        PurgeUnverifiedAccountsCommand command,
        CancellationToken ct = default
    )
    {
        var expired = await users.ListUnverifiedRegisteredUpToAsync(
            User.UnverifiedPurgeCutoff(clock.UtcNow),
            ct
        );

        return await expired
            .ToAsyncEnumerable()
            .CountAsync(async (account, token) => await eraser.EraseAsync(account, token), ct);
    }
}
