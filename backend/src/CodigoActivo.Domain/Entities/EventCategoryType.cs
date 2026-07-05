using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class EventCategoryType : IdentifiableEntity
{
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;

    public ICollection<EventCategory> Events { get; set; } = [];
}