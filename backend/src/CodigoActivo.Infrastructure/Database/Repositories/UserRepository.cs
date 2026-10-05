using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Stores and loads accounts. Accounts are only deleted through the account erasure, which keeps
/// the legal copy.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class UserRepository(CodigoActivoDbContext context)
    : AggregateRepository<User>(context),
        IUserRepository
{
    /// <inheritdoc />
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return Set.FirstOrDefaultAsync(user => user.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<User>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    )
    {
        return await Set.Where(user => ids.Contains(user.Id)).ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return Set.FirstOrDefaultAsync(user => user.Email == email, ct);
    }

    /// <inheritdoc />
    public Task<int> CountDependentsAsync(Guid guardianId, CancellationToken ct = default)
    {
        return Set.CountAsync(user => user.ParentId == guardianId, ct);
    }

    /// <inheritdoc />
    public Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId = null,
        CancellationToken ct = default
    )
    {
        return Set.AnyAsync(
            user => user.Email == email && (excludeUserId == null || user.Id != excludeUserId),
            ct
        );
    }

    /// <inheritdoc />
    public async Task<bool> LockPasswordStateAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        FormattableString sql = $"""
            SELECT
                password_failed_attempts AS attempts,
                password_locked_at AS locked_at,
                login_challenge_id AS challenge_id
            FROM users
            WHERE id = {user.Id}
            FOR UPDATE
            """;
        var persisted = await Context
            .Database.SqlQuery<PasswordState>(sql)
            .SingleOrDefaultAsync(ct);
        if (persisted is null)
        {
            return false;
        }

        var entry = Context.Entry(user);
        MarkPersisted(entry.Property(u => u.PasswordFailedAttempts), persisted.Attempts);
        MarkPersisted(entry.Property(u => u.PasswordLockedAt), persisted.LockedAt);
        MarkPersisted(entry.Property(u => u.LoginChallengeId), persisted.ChallengeId);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> LockAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        FormattableString sql = $"""
            SELECT id AS "Value" FROM users WHERE id = {user.Id} FOR UPDATE
            """;
        if ((await Context.Database.SqlQuery<Guid>(sql).ToListAsync(ct)).Count is 0)
        {
            return false;
        }

        await Context.Entry(user).ReloadAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task SavePasswordStateAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var attempts = user.PasswordFailedAttempts;
        var lockedAt = user.PasswordLockedAt;
        var challengeId = user.LoginChallengeId;
        await Set.Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(u => u.PasswordFailedAttempts, attempts)
                        .SetProperty(u => u.PasswordLockedAt, lockedAt)
                        .SetProperty(u => u.LoginChallengeId, challengeId),
                ct
            );

        var entry = Context.Entry(user);
        MarkPersisted(entry.Property(u => u.PasswordFailedAttempts), attempts);
        MarkPersisted(entry.Property(u => u.PasswordLockedAt), lockedAt);
        MarkPersisted(entry.Property(u => u.LoginChallengeId), challengeId);
    }

    private static void MarkPersisted<TProperty>(
        PropertyEntry<User, TProperty> property,
        TProperty value
    )
    {
        property.CurrentValue = value;
        if (property.EntityEntry.State is EntityState.Unchanged or EntityState.Modified)
        {
            property.OriginalValue = value;
            property.IsModified = false;
        }
    }

    internal sealed record PasswordState
    {
        public int Attempts { get; init; }

        public DateTimeOffset? LockedAt { get; init; }

        public Guid? ChallengeId { get; init; }
    }
}
