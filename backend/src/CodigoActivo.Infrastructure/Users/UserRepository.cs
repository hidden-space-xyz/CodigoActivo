using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Npgsql;
using NpgsqlTypes;

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
    private IQueryable<User> Accounts => Set.Include(user => user.PromotionalConsentChanges);

    /// <inheritdoc />
    public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default)
    {
        return Accounts.FirstOrDefaultAsync(user => user.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<User>> ListByIdsAsync(
        IReadOnlyCollection<UserId> ids,
        CancellationToken ct = default
    )
    {
        return await Accounts.Where(user => ids.Contains(user.Id)).ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<User?> GetByEmailAsync(EmailAddress email, CancellationToken ct = default)
    {
        return Accounts.FirstOrDefaultAsync(user => user.Email == email, ct);
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
        return await Accounts.Where(user => user.ParentId == guardianId).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<User>> ListUnverifiedRegisteredUpToAsync(
        DateTimeOffset cutoff,
        CancellationToken ct = default
    )
    {
        return await Accounts
            .Where(user => user.Status == UserStatus.Pending && user.CreatedAt <= cutoff)
            .OrderBy(user => user.CreatedAt)
            .ToListAsync(ct);
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
    /// <remarks>
    /// When the row still has the version the account was loaded with, the write returns the new
    /// version and the account tracks it, so a later save of its other changes does not take this
    /// write for a concurrent one. Otherwise the row changed since it was loaded: the state is
    /// stored all the same, so no failed attempt is lost, and the account keeps its old version,
    /// so that later save still fails.
    /// </remarks>
    public async Task SavePasswordStateAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var lockout = user.PasswordLockout;
        var challenge = user.LoginChallenge;
        var entry = Context.Entry(user);
        if (!await TryWritePasswordStateOverLoadedVersionAsync(entry, ct))
        {
            await Set.Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(u => u.PasswordLockout, lockout)
                            .SetProperty(u => u.LoginChallenge, challenge),
                    ct
                );
        }

        MarkPersisted(entry.ComplexProperty(u => u.PasswordLockout), lockout);
        MarkPersisted(entry.ComplexProperty(u => u.LoginChallenge), challenge);
        await unitOfWork.PublishAfterCommitAsync(user.PullDomainEvents<PasswordLocked>());
    }

    private async Task<bool> TryWritePasswordStateOverLoadedVersionAsync(
        EntityEntry<User> entry,
        CancellationToken ct
    )
    {
        var user = entry.Entity;
        var lockout = user.PasswordLockout;
        var challenge = user.LoginChallenge;
        var version = entry.Property<uint>(UserConfiguration.RowVersion);
        var loadedVersion = new NpgsqlParameter<uint>
        {
            NpgsqlDbType = NpgsqlDbType.Xid,
            TypedValue = version.OriginalValue,
        };
        FormattableString sql = $"""
            UPDATE users
            SET
                password_failed_attempts = {lockout.FailedAttempts},
                password_locked_at = {lockout.LockedAt},
                login_challenge_id = {challenge.Id?.Value},
                login_challenge_started_at = {challenge.StartedAt}
            WHERE id = {user.Id.Value} AND xmin = {loadedVersion}
            RETURNING xmin::text::bigint AS "Value"
            """;
        if (await Context.Database.SqlQuery<long>(sql).ToListAsync(ct) is not [var stored])
        {
            return false;
        }

        var current = checked((uint)stored);
        version.CurrentValue = current;
        version.OriginalValue = current;
        version.IsModified = false;
        return true;
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
