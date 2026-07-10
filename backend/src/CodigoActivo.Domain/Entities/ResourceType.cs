using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class ResourceType : NamedEntity
{
    public string Color { get; set; } = null!;
    public bool IsExternal { get; set; }

    public ICollection<Resource> Resources { get; set; } = [];
}
