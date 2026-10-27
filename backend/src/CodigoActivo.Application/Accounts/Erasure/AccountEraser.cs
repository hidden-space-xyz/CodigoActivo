using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Erasure;

/// <summary>
/// Erases an account and its dependents in one transaction: it credits the content the household
/// authored to the <see cref="InitialAdministrator"/>, deletes the email still waiting for their
/// addresses and deletes the accounts with everything that belongs to them. Every deleted or
/// handed-over row leaves its data history, which the law
/// requires to keep blocked (LOPDGDD article 32). Any other change staged in the unit of work
/// commits with it.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="erasureStore">Storage steps that work on the whole household.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class AccountEraser(
    IUserRepository users,
    IAccountErasureStore erasureStore,
    IUnitOfWork uow
)
{
    /// <summary>
    /// Erases an account.
    /// </summary>
    /// <param name="account">Account to erase, tracked by the unit of work.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="false"/> when the account was erased meanwhile.</returns>
    /// <exception cref="InvalidOperationException">The account is the initial administrator.</exception>
    public Task<bool> EraseAsync(User account, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.Id == InitialAdministrator.Id)
        {
            throw new InvalidOperationException(
                "The initial administrator can never be deleted: it keeps the application administered and owns the content of erased accounts."
            );
        }

        return uow.ExecuteInTransactionAsync(
            async attempt =>
            {
                if (!await erasureStore.LockHouseholdAsync(account.Id, attempt))
                {
                    return false;
                }

                await erasureStore.LoadHouseholdAsync(account.Id, attempt);
                await erasureStore.HandOverAuthoredContentAsync(
                    account.Id,
                    InitialAdministrator.Id,
                    attempt
                );
                await erasureStore.DiscardPendingEmailAsync(account.Id, attempt);
                account.Erase();
                users.Remove(account);
                await uow.SaveChangesAsync(attempt);
                return true;
            },
            ct
        );
    }
}
