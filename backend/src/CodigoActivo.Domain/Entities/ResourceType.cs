using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class ResourceType : NamedEntity
{
    public required string Color { get; set; }
    public bool IsExternal { get; set; }

    public ICollection<Resource> Resources { get; set; } = [];
}
