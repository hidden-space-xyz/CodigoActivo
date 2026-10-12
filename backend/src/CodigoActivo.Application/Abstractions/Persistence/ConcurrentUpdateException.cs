namespace CodigoActivo.Application.Abstractions.Persistence;

/// <summary>
/// Represents a commit that the database rejected because a row it updates or deletes was written
/// or removed by a concurrent transaction after the use case read it, so its decisions rest on
/// values that no longer hold. The refused commit stores nothing; the request can be sent again.
/// </summary>
public sealed class ConcurrentUpdateException : Exception
{
    private const string DefaultMessage =
        "Another transaction changed the data after it was read, so the changes were not stored.";

    /// <summary>
    /// Initializes a concurrent update without identifying the affected entity.
    /// </summary>
    public ConcurrentUpdateException()
        : base(DefaultMessage) { }

    /// <summary>
    /// Initializes a concurrent update with a diagnostic message.
    /// </summary>
    /// <param name="message">Diagnostic message; it must not contain personal data.</param>
    public ConcurrentUpdateException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a concurrent update with a diagnostic message and its cause.
    /// </summary>
    /// <param name="message">Diagnostic message; it must not contain personal data.</param>
    /// <param name="innerException">Database error that rejected the commit.</param>
    public ConcurrentUpdateException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Initializes a concurrent update for the entity whose row changed since it was read.
    /// </summary>
    /// <param name="entityType">Entity type stored in the row that changed, if known.</param>
    /// <param name="innerException">Database error that rejected the commit.</param>
    public ConcurrentUpdateException(Type? entityType, Exception innerException)
        : base(DefaultMessage, innerException)
    {
        EntityType = entityType;
    }

    /// <summary>
    /// Gets the entity type stored in the row that changed since it was read;
    /// <see langword="null"/> when it is unknown.
    /// </summary>
    public Type? EntityType { get; }
}
