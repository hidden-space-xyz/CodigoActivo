using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class ActivityModalityType : IdentifiableEntity
{
    public string Name { get; set; } = null!;

    public ICollection<Activity> Activities { get; set; } = [];
}