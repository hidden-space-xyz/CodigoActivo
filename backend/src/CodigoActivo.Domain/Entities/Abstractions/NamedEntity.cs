namespace CodigoActivo.Domain.Entities.Abstractions;

public abstract class NamedEntity : IdentifiableEntity
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
}