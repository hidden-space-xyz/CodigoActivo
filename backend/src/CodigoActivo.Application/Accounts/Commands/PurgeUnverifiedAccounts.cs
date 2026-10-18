using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
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
/// Executes the command that erases, with their dependents and a blocked copy, the accounts still
/// unverified <see cref="User.UnverifiedRetentionDays"/> after their registration. Each account is
/// locked and checked again in its own transaction, so one verified meanwhile is kept.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="eraser">Eraser that deletes an account through its blocked copy.</param>
/// <param name="uow">Unit of work that runs each erasure in its own transaction.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class PurgeUnverifiedAccountsCommandHandler(
    IUserRepository users,
    AccountEraser eraser,
    IUnitOfWork uow,
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
        var now = clock.UtcNow;
        var expired = await users.ListUnverifiedRegisteredUpToAsync(
            User.UnverifiedPurgeCutoff(now),
            ct
        );

        var erased = 0;
        foreach (var account in expired)
        {
            if (
                await uow.ExecuteInTransactionAsync(
                    attempt => EraseIfStillExpiredAsync(account, now, attempt),
                    ct
                )
            )
            {
                erased++;
            }
        }

        return erased;
    }

    private async Task<bool> EraseIfStillExpiredAsync(
        User account,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        return await users.LockAsync(account, ct)
            && account.HasVerificationExpired(now)
            && await eraser.EraseAsync(
                account,
                AccountErasure.ForExpiredVerification(account, now),
                ct
            );
    }
}
