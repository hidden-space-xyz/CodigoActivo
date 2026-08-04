namespace CodigoActivo.Domain.Entities.Abstractions;

public abstract class NamedEntity : IdentifiableEntity
{
    public required string Name { get; set; }
    public required string Description { get; set; }
}
