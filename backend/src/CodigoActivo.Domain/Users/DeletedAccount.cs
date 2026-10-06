using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// Blocked copy of an erased account that the law requires to keep: the personal data of the
/// account and its dependents, their activity assignments and the terms decisions behind them, as
/// they stood when the account was deleted. Nothing in the application reads it; the row is only
/// reachable directly in the database and is purged once <see cref="RetentionYears"/> have passed.
/// </summary>
public class DeletedAccount : AggregateRoot<UserId>
{
    /// <summary>
    /// Years a copy is kept before it is physically purged.
    /// </summary>
    public const int RetentionYears = 2;

    private DeletedAccount() { }

    /// <summary>
    /// Gets the UTC timestamp when the account was deleted.
    /// </summary>
    public DateTimeOffset DeletedAt { get; private set; }

    /// <summary>
    /// Gets the JSON document holding the copy.
    /// </summary>
    public string Data { get; private set; } = "{}";

    /// <summary>
    /// Records the copy of an account being erased.
    /// </summary>
    /// <param name="accountId">Identifier of the erased account.</param>
    /// <param name="erasure">Who erased it and when.</param>
    /// <param name="legalCopy">JSON document holding the copy.</param>
    /// <returns>The copy to keep.</returns>
    public static DeletedAccount Record(UserId accountId, AccountErasure erasure, string legalCopy)
    {
        ArgumentNullException.ThrowIfNull(erasure);
        ArgumentException.ThrowIfNullOrWhiteSpace(legalCopy);

        var deleted = new DeletedAccount
        {
            Id = accountId,
            DeletedAt = erasure.DeletedAt,
            Data = legalCopy,
        };
        deleted.Raise(new AccountErased(accountId));
        return deleted;
    }

    /// <summary>
    /// Tells up to which deletion moment copies have been kept long enough to be purged.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns>The latest deletion moment whose copy may be purged.</returns>
    public static DateTimeOffset PurgeCutoff(DateTimeOffset now)
    {
        return now.AddYears(-RetentionYears);
    }
}
