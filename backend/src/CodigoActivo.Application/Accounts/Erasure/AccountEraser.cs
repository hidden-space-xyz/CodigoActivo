using CodigoActivo.Application.TermsDecisions;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Erasure;

/// <summary>
/// Erases an account and its dependents in one transaction: it credits the content the household
/// authored to the <see cref="InitialAdministrator"/>, deletes the email still waiting for their
/// addresses, tells the association's legal contact about the withdrawn consents it deletes before
/// they were told and about the consents given it deletes on events that have already started, and
/// deletes the accounts with everything that belongs to them. When the account is the one
/// designated for data protection, the designation returns to the initial administrator in the
/// same transaction. Every deleted or handed-over row leaves its data history, which the law
/// requires to keep blocked (LOPDGDD article 32). Any other change staged in the unit of work
/// commits with it.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="erasureStore">Storage steps that work on the whole household.</param>
/// <param name="withdrawalNotices">Notifier that tells the legal contact about withdrawals.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock that dates the return of the data protection designation.</param>
public sealed class AccountEraser(
    IUserRepository users,
    IAccountErasureStore erasureStore,
    ConsentWithdrawalNotifier withdrawalNotices,
    IUnitOfWork uow,
    IClock clock
)
{
    /// <summary>
    /// Erases an account.
    /// </summary>
    /// <param name="account">Account to erase, tracked by the unit of work.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is <see langword="false"/> when the account was erased meanwhile.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The account is the initial administrator.
    /// </exception>
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
                if (!await erasureStore.LockAccountAsync(account.Id, attempt))
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
                await withdrawalNotices.NotifyAsync(
                    await erasureStore.ListWithdrawalNoticesPendingAsync(account.Id, attempt),
                    attempt
                );
                await withdrawalNotices.NotifyErasedAsync(
                    await erasureStore.ListAcceptedDecisionsAsync(account.Id, attempt),
                    attempt
                );
                account.Erase();
                users.Remove(account);
                await uow.SaveChangesAsync(attempt);
                if (account.IsPrivacyOfficer)
                {
                    await ReturnPrivacyOfficerAsync(attempt);
                }

                return true;
            },
            ct
        );
    }

    private async Task ReturnPrivacyOfficerAsync(CancellationToken ct)
    {
        var successor =
            await users.GetByIdAsync(InitialAdministrator.Id, ct)
            ?? throw new InvalidOperationException("The initial administrator always exists.");
        if (successor.DesignateAsPrivacyOfficer(clock.UtcNow).IsFailure)
        {
            throw new InvalidOperationException(
                "The initial administrator always accepts the data protection designation."
            );
        }

        await uow.SaveChangesAsync(ct);
    }
}
