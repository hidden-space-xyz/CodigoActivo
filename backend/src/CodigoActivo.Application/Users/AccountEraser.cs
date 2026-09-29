using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users;

/// <summary>
/// Erases an account and its dependents in one transaction: it keeps the legal copy the law
/// requires, credits the content the household authored to the
/// <see cref="InitialAdministrator"/>, and deletes the accounts with everything that belongs to
/// them. Any other change staged in the unit of work commits with it.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="deletedAccounts">Repository of the legal copies.</param>
/// <param name="erasureStore">Storage steps that work on the whole household.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class AccountEraser(
    IUserRepository users,
    IDeletedAccountRepository deletedAccounts,
    IAccountErasureStore erasureStore,
    IUnitOfWork uow
)
{
    /// <summary>
    /// Erases an account.
    /// </summary>
    /// <param name="account">Account to erase, tracked by the unit of work.</param>
    /// <param name="erasure">Who erases it and when.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="false"/> when the account was erased meanwhile.</returns>
    public Task<bool> EraseAsync(
        User account,
        AccountErasure erasure,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(erasure);

        return uow.ExecuteInTransactionAsync(
            async attempt =>
            {
                if (!await erasureStore.LockHouseholdAsync(account.Id, attempt))
                {
                    return false;
                }

                var legalCopy = await erasureStore.CaptureLegalCopyAsync(
                    account.Id,
                    erasure,
                    attempt
                );
                await erasureStore.HandOverAuthoredContentAsync(
                    account.Id,
                    InitialAdministrator.Id,
                    attempt
                );
                await deletedAccounts.AddAsync(
                    DeletedAccount.Record(account.Id, erasure, legalCopy),
                    attempt
                );
                users.Remove(account);
                await uow.SaveChangesAsync(attempt);
                return true;
            },
            ct
        );
    }
}
