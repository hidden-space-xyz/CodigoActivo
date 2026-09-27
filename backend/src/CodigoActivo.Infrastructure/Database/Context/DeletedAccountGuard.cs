using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CodigoActivo.Infrastructure.Database.Context;

/// <summary>
/// Refuses any commit that deletes the initial administrator, any commit that deletes a user
/// without the <see cref="DeletedAccount"/> copy that <see cref="IDeletedAccountRepository.EraseAsync"/>
/// adds first, and any commit that changes or deletes a stored copy. A dependent deleted together
/// with an archived guardian is covered by the guardian's copy. Set-based deletes never reach the
/// change tracker: <c>RemoveAsync</c> on users throws and the retention purge is the only set-based
/// delete of copies.
/// </summary>
public sealed class DeletedAccountGuard : SaveChangesInterceptor
{
    private DeletedAccountGuard() { }

    /// <summary>
    /// Gets the shared stateless instance.
    /// </summary>
    public static DeletedAccountGuard Instance { get; } = new();

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result
    )
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Check(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Check(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void Check(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var archived = new HashSet<Guid>();
        var deletedUsers = new List<User>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            switch (entry.Entity)
            {
                case DeletedAccount copy when entry.State is EntityState.Added:
                    archived.Add(copy.Id);
                    break;
                case DeletedAccount when entry.State is EntityState.Modified or EntityState.Deleted:
                    throw new InvalidOperationException(
                        "A deleted account copy cannot be changed or deleted; only the retention purge removes it."
                    );
                case User user when entry.State is EntityState.Deleted:
                    deletedUsers.Add(user);
                    break;
            }
        }

        if (deletedUsers.Exists(user => user.Id == SeedIds.Users.InitialAdministrator))
        {
            throw new InvalidOperationException(
                "The initial administrator can never be deleted: it keeps the application administered and owns the content of erased accounts."
            );
        }

        if (deletedUsers.Exists(user => !IsArchived(user, archived)))
        {
            throw new InvalidOperationException(
                "A user can only be deleted through IDeletedAccountRepository.EraseAsync, which stores the legal copy first."
            );
        }
    }

    private static bool IsArchived(User user, HashSet<Guid> archived)
    {
        return archived.Contains(user.Id)
            || (user.ParentId is { } guardianId && archived.Contains(guardianId));
    }
}
