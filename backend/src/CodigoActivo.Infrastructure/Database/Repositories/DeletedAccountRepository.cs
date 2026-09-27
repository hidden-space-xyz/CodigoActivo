using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Erases accounts together with their legal copy and purges the copies whose retention ended.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class DeletedAccountRepository(
    CodigoActivoDbContext context,
    ILogger<DeletedAccountRepository> logger
) : IDeletedAccountRepository
{
    /// <summary>
    /// Attempts made when PostgreSQL resolves a deadlock by aborting the erasure, which happens
    /// when a concurrent signup of the household takes its row locks in the opposite order.
    /// </summary>
    internal const int MaxAttempts = 3;

    /// <inheritdoc />
    public async Task<bool> EraseAsync(
        User user,
        AccountErasure erasure,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(erasure);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await TryEraseAsync(user, erasure, ct);
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsDeadlock(ex))
            {
                logger.AccountErasureDeadlockRetried(attempt);
            }
        }
    }

    /// <inheritdoc />
    public Task<int> PurgeAsync(DateTimeOffset deletedUpTo, CancellationToken ct = default)
    {
        return context
            .DeletedAccounts.Where(copy => copy.DeletedAt <= deletedUpTo)
            .ExecuteDeleteAsync(ct);
    }

    private async Task<bool> TryEraseAsync(User user, AccountErasure erasure, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        if (!await LockHouseholdAsync(user.Id, ct))
        {
            return false;
        }

        var copy = new DeletedAccount
        {
            Id = user.Id,
            DeletedAt = erasure.DeletedAt,
            Data = await DeletedAccountSnapshot.BuildAsync(context, user.Id, erasure, ct),
        };
        context.DeletedAccounts.Add(copy);
        context.Users.Remove(user);
        try
        {
            await ((IUnitOfWork)context).SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            context.Entry(copy).State = EntityState.Detached;
            throw;
        }

        return true;
    }

    /// <summary>
    /// Locks, until the transaction ends, the user first, then their dependents, then every
    /// assignment and terms decision of the household. Once the user is locked no dependent can be
    /// added, and every row the copy reads is locked before it is read, so nothing is added,
    /// changed or removed between the copy and the deletion: those writes wait and then fail.
    /// </summary>
    /// <param name="userId">Identifier of the user being erased.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="false"/> when the user no longer exists.</returns>
    private async Task<bool> LockHouseholdAsync(Guid userId, CancellationToken ct)
    {
        FormattableString account = $"""
            SELECT id AS "Value" FROM users WHERE id = {userId} FOR UPDATE
            """;
        if ((await context.Database.SqlQuery<Guid>(account).ToListAsync(ct)).Count is 0)
        {
            return false;
        }

        await context.Database.ExecuteSqlAsync(
            $"""
            SELECT id FROM users WHERE parent_id = {userId} FOR UPDATE;
            SELECT a.user_id
            FROM activity_user_role_assignments AS a
            JOIN users AS u ON u.id = a.user_id
            WHERE u.id = {userId} OR u.parent_id = {userId}
            FOR UPDATE OF a;
            SELECT t.user_id
            FROM event_terms_acceptances AS t
            JOIN users AS u ON u.id = t.user_id
            WHERE u.id = {userId} OR u.parent_id = {userId}
            FOR UPDATE OF t
            """,
            ct
        );
        return true;
    }

    private static bool IsDeadlock(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected })
            {
                return true;
            }
        }

        return false;
    }
}
