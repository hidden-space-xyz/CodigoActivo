namespace CodigoActivo.Domain.Entities.Abstractions;

public abstract class IdentifiableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}