using CodigoActivo.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CodigoActivo.Infrastructure.Database.Context;

/// <summary>
/// Refuses any commit that deletes the initial administrator, whatever path staged the deletion.
/// </summary>
public sealed class InitialAdministratorGuard : SaveChangesInterceptor
{
    private InitialAdministratorGuard() { }

    /// <summary>
    /// Gets the shared stateless instance.
    /// </summary>
    public static InitialAdministratorGuard Instance { get; } = new();

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
        if (
            context is not null
            && context
                .ChangeTracker.Entries<User>()
                .Any(entry =>
                    entry.State is EntityState.Deleted && entry.Entity.Id == InitialAdministrator.Id
                )
        )
        {
            throw new InvalidOperationException(
                "The initial administrator can never be deleted: it keeps the application administered and owns the content of erased accounts."
            );
        }
    }
}
