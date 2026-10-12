using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CodigoActivo.Infrastructure.Users;

/// <summary>
/// Stores and loads accounts. Accounts are only deleted through the account erasure, which keeps
/// the legal copy.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
/// <param name="unitOfWork">Unit of work that publishes the events of the lockouts stored here.</param>
public class UserRepository(CodigoActivoDbContext context, UnitOfWork unitOfWork)
    : AggregateRepository<User>(context),
        IUserRepository
{
    /// <inheritdoc />
    public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default)
    {
        return Set.FirstOrDefaultAsync(user => user.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<User>> ListByIdsAsync(
        IReadOnlyCollection<UserId> ids,
        CancellationToken ct = default
    )
    {
        return await Set.Where(user => ids.Contains(user.Id)).ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<User?> GetByEmailAsync(EmailAddress email, CancellationToken ct = default)
    {
        return Set.FirstOrDefaultAsync(user => user.Email == email, ct);
    }

    /// <inheritdoc />
    public Task<int> CountDependentsAsync(UserId guardianId, CancellationToken ct = default)
    {
        return Set.CountAsync(user => user.ParentId == guardianId, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<User>> ListDependentsAsync(
        UserId guardianId,
        CancellationToken ct = default
    )
    {
        return await Set.Where(user => user.ParentId == guardianId).ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> EmailExistsAsync(
        EmailAddress email,
        UserId? excludeUserId = null,
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
                login_challenge_id AS challenge_id,
                login_challenge_started_at AS challenge_started_at
            FROM users
            WHERE id = {user.Id.Value}
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
        MarkPersisted(
            entry.ComplexProperty(u => u.PasswordLockout),
            new PasswordLockout(persisted.Attempts, persisted.LockedAt)
        );
        MarkPersisted(
            entry.ComplexProperty(u => u.LoginChallenge),
            new LoginChallenge(
                persisted.ChallengeId is { } challengeId
                    ? LoginChallengeId.From(challengeId)
                    : null,
                persisted.ChallengeStartedAt
            )
        );
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> LockAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        FormattableString sql = $"""
            SELECT id AS "Value" FROM users WHERE id = {user.Id.Value} FOR UPDATE
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

        var lockout = user.PasswordLockout;
        var challenge = user.LoginChallenge;
        await Set.Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(u => u.PasswordLockout, lockout)
                        .SetProperty(u => u.LoginChallenge, challenge),
                ct
            );

        var entry = Context.Entry(user);
        MarkPersisted(entry.ComplexProperty(u => u.PasswordLockout), lockout);
        MarkPersisted(entry.ComplexProperty(u => u.LoginChallenge), challenge);
        await unitOfWork.PublishAfterCommitAsync(user.PullDomainEvents<PasswordLocked>());
    }

    private static void MarkPersisted<TComplex>(
        ComplexPropertyEntry<User, TComplex> complexProperty,
        TComplex value
    )
    {
        complexProperty.CurrentValue = value;
        if (complexProperty.EntityEntry.State is EntityState.Unchanged or EntityState.Modified)
        {
            foreach (var property in complexProperty.Properties)
            {
                property.OriginalValue = property.CurrentValue;
                property.IsModified = false;
            }
        }
    }

    internal sealed record PasswordState
    {
        public int Attempts { get; init; }

        public DateTimeOffset? LockedAt { get; init; }

        public Guid? ChallengeId { get; init; }

        public DateTimeOffset? ChallengeStartedAt { get; init; }
    }
}
