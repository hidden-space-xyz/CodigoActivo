using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class ActivityModalityType : IdentifiableEntity
{
    public required string Name { get; set; }

    public ICollection<Activity> Activities { get; set; } = [];
}
