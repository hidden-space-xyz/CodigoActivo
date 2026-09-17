namespace CodigoActivo.Domain.Common;

/// <summary>
/// Represents a persistence-layer unique constraint violation translated by the infrastructure layer,
/// so application handlers can react to a duplicate write without depending on database driver types.
/// </summary>
public sealed class UniqueConstraintViolationException : Exception
{
    /// <summary>
    /// Initializes a unique constraint violation with the offending constraint name and the original
    /// persistence exception.
    /// </summary>
    /// <param name="constraintName">Name of the database constraint that rejected the write, when known.</param>
    /// <param name="innerException">Original exception raised by the persistence layer.</param>
    public UniqueConstraintViolationException(string? constraintName, Exception innerException)
        : base("A unique constraint was violated while saving changes.", innerException)
    {
        ConstraintName = constraintName;
    }

    /// <summary>
    /// Gets the name of the database constraint that rejected the write, when known.
    /// </summary>
    public string? ConstraintName { get; }
}
