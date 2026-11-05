using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the request to drop the email changes and authenticator enrollments that can no longer
/// be confirmed.
/// </summary>
public sealed record DiscardExpiredAccountChangesCommand : ICommand<Result<int>>, IScheduledCommand;

/// <summary>
/// Executes the command that drops, through <see cref="User.DiscardExpiredChanges"/>, the address
/// asked for by an email change whose code expired and the secret of an authenticator enrollment
/// nobody confirmed in time. The address leaves its data history, blocked like any other erased
/// data; the secret, like every authenticator secret, does not. Each account is locked and checked
/// again in its own transaction, so a change requested meanwhile is kept.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work that saves each account in its own transaction.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class DiscardExpiredAccountChangesCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock
) : ICommandHandler<DiscardExpiredAccountChangesCommand, Result<int>>
{
    /// <summary>
    /// Handles the request to drop the expired email changes and authenticator enrollments.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result carries the number of accounts changed.</returns>
    public async Task<Result<int>> HandleAsync(
        DiscardExpiredAccountChangesCommand command,
        CancellationToken ct = default
    )
    {
        var now = clock.UtcNow;
        var expired = await users.ListWithExpiredChangesAsync(now, ct);

        return await expired
            .ToAsyncEnumerable()
            .CountAsync(
                async (account, token) =>
                    await uow.ExecuteInTransactionAsync(
                        attempt => DiscardIfStillExpiredAsync(account, now, attempt),
                        token
                    ),
                ct
            );
    }

    private async Task<bool> DiscardIfStillExpiredAsync(
        User account,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        if (!await users.LockAsync(account, ct) || !account.DiscardExpiredChanges(now))
        {
            return false;
        }

        await uow.SaveChangesAsync(ct);
        return true;
    }
}
