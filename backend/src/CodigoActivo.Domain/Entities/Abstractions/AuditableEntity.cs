namespace CodigoActivo.Domain.Entities.Abstractions;

public abstract class AuditableEntity : IdentifiableEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
