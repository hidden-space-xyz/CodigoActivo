namespace CodigoActivo.Domain.Common;

/// <summary>
/// Entity that records who created it and who changed it last, and when.
/// </summary>
public abstract class AuditableEntity : IdentifiableEntity
{
    /// <summary>
    /// Gets the UTC timestamp when the entity was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp of the most recent update.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>
    /// Gets the identifier of the user who created the entity.
    /// </summary>
    public Guid CreatedBy { get; private set; }

    /// <summary>
    /// Gets the identifier of the user who changed the entity last.
    /// </summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>
    /// Records who created the entity and when.
    /// </summary>
    /// <param name="authorId">Identifier of the user who creates it.</param>
    /// <param name="now">Current time.</param>
    protected void RecordCreation(Guid authorId, DateTimeOffset now)
    {
        CreatedBy = authorId;
        CreatedAt = now;
    }

    /// <summary>
    /// Records who changed the entity last and when.
    /// </summary>
    /// <param name="editorId">Identifier of the user who changes it.</param>
    /// <param name="now">Current time.</param>
    protected void RecordUpdate(Guid editorId, DateTimeOffset now)
    {
        UpdatedBy = editorId;
        UpdatedAt = now;
    }
}
