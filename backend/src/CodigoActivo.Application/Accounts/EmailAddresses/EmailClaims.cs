using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.Erasure;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.EmailAddresses;

/// <summary>
/// Commits an account that takes an email: a new registration, or an account whose holder confirmed
/// the address as their new email. An account nobody verified that held the address is erased in
/// the same transaction, keeping its legal copy; one that <see cref="User.OwnsEmail"/> is never
/// erased, so callers turn such claims away first. The holder is locked and reloaded before it is
/// erased, so a holder verified or moved to another address meanwhile is seen as it is now: one
/// that verified the address keeps it and the claim is refused, and one that no longer holds it is
/// left alone.
/// </summary>
/// <param name="users">Repository that locks and reloads the holder.</param>
/// <param name="eraser">Eraser of the account that loses the email.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class EmailClaims(IUserRepository users, AccountEraser eraser, IUnitOfWork uow)
{
    /// <summary>
    /// Commits every staged change, <paramref name="claimant"/> taking the email among them.
    /// </summary>
    /// <param name="claimant">Account that takes the email, staged in the unit of work.</param>
    /// <param name="unverifiedHolder">Account nobody verified that holds the email, if any.</param>
    /// <param name="now">Current time.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is <see langword="false"/> when another account took the email first
    /// and nothing was committed.
    /// </returns>
    /// <exception cref="ArgumentException">The holder owns the email.</exception>
    public async Task<bool> TryCommitAsync(
        User claimant,
        User? unverifiedHolder,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(claimant);
        if (unverifiedHolder is { OwnsEmail: true })
        {
            throw new ArgumentException(
                "An account that owns its email cannot lose it.",
                nameof(unverifiedHolder)
            );
        }

        bool claimed;
        try
        {
            claimed = await uow.ExecuteInTransactionAsync(
                attempt => ClaimAsync(claimant, unverifiedHolder, now, attempt),
                ct
            );
        }
        catch (UniqueConstraintViolationException ex) when (ex.EntityType == typeof(User))
        {
            claimed = false;
        }

        if (!claimed)
        {
            uow.DiscardChanges();
        }

        return claimed;
    }

    private async Task<bool> ClaimAsync(
        User claimant,
        User? holder,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        if (
            holder is not null
            && await users.LockAsync(holder, ct)
            && holder.Email == claimant.Email
        )
        {
            if (holder.OwnsEmail)
            {
                return false;
            }

            if (
                await eraser.EraseAsync(
                    holder,
                    AccountErasure.ForClaimedEmail(claimant.Id, now),
                    ct
                )
            )
            {
                return true;
            }
        }

        await uow.SaveChangesAsync(ct);
        return true;
    }
}
