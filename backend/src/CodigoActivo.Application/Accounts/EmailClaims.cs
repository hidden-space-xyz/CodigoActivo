using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Users;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Commits an account that takes an email: a new registration, or an account whose holder confirmed
/// the address as their new email. An account nobody verified that held the address is erased in
/// the same transaction, keeping its legal copy; one that <see cref="User.OwnsEmail"/> is never
/// erased, so callers turn such claims away first.
/// </summary>
/// <param name="eraser">Eraser of the account that loses the email.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class EmailClaims(AccountEraser eraser, IUnitOfWork uow)
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

        try
        {
            var erased =
                unverifiedHolder is not null
                && await eraser.EraseAsync(
                    unverifiedHolder,
                    AccountErasure.ForClaimedEmail(claimant.Id, now),
                    ct
                );
            if (!erased)
            {
                await uow.SaveChangesAsync(ct);
            }

            return true;
        }
        catch (UniqueConstraintViolationException ex) when (ex.EntityType == typeof(User))
        {
            uow.DiscardChanges();
            return false;
        }
    }
}
