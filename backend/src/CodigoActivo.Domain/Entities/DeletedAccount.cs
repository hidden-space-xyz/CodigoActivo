namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the blocked copy of an erased account that the law requires to keep: the personal
/// data of the account and its dependents, their activity assignments and the terms decisions
/// behind them, as they stood when the account was deleted. Nothing in the application reads it;
/// the row is only reachable directly in the database and is purged once
/// <see cref="RetentionYears"/> have passed.
/// </summary>
public class DeletedAccount
{
    /// <summary>
    /// Years a copy is kept before it is physically purged.
    /// </summary>
    public const int RetentionYears = 2;

    /// <summary>
    /// Gets or sets the identifier the user had. It is not a foreign key, since the user row no
    /// longer exists.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the account was deleted.
    /// </summary>
    public DateTimeOffset DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the JSON document holding the copy.
    /// </summary>
    public string Data { get; set; } = "{}";
}
