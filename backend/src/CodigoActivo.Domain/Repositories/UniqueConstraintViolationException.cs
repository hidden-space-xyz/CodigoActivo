namespace CodigoActivo.Domain.Repositories;

/// <summary>
/// Represents a commit that the database rejected because it would duplicate a value that must be
/// unique, typically written by a concurrent request after the handler checked it was still free.
/// </summary>
public sealed class UniqueConstraintViolationException : Exception
{
    private const string DefaultMessage =
        "The changes would duplicate a value that must be unique.";

    /// <summary>
    /// Initializes a unique constraint violation without identifying the affected entity.
    /// </summary>
    public UniqueConstraintViolationException()
        : base(DefaultMessage) { }

    /// <summary>
    /// Initializes a unique constraint violation with a diagnostic message.
    /// </summary>
    /// <param name="message">Diagnostic message; it must not contain personal data.</param>
    public UniqueConstraintViolationException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a unique constraint violation with a diagnostic message and its cause.
    /// </summary>
    /// <param name="message">Diagnostic message; it must not contain personal data.</param>
    /// <param name="innerException">Database error that rejected the commit.</param>
    public UniqueConstraintViolationException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Initializes a unique constraint violation for the entity whose table rejected the commit.
    /// </summary>
    /// <param name="entityType">Entity type stored in the table that rejected the commit, if known.</param>
    /// <param name="innerException">Database error that rejected the commit.</param>
    public UniqueConstraintViolationException(Type? entityType, Exception innerException)
        : base(DefaultMessage, innerException)
    {
        EntityType = entityType;
    }

    /// <summary>
    /// Gets the entity type stored in the table that rejected the commit, so a handler can tell
    /// a duplicate it expects from an unrelated one; <see langword="null"/> when it is unknown.
    /// </summary>
    public Type? EntityType { get; }
}
