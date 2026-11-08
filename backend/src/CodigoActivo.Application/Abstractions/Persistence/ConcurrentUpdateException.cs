namespace CodigoActivo.Application.Abstractions.Persistence;

/// <summary>
/// Represents a commit that the database rejected because a row it updates or deletes was written
/// or removed by a concurrent transaction after the use case read it, so its decisions rest on
/// values that no longer hold, or because it aborted the transaction to resolve a deadlock with a
/// concurrent one. The refused commit stores nothing; the request can be sent again.
/// </summary>
public sealed class ConcurrentUpdateException : Exception
{
    private const string DefaultMessage =
        "Another transaction changed the data after it was read, so the changes were not stored.";

    /// <summary>
    /// Initializes a concurrent update with the default diagnostic message.
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
}
