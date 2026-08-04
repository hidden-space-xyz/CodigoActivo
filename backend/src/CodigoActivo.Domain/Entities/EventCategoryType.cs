using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class EventCategoryType : IdentifiableEntity
{
    public required string Name { get; set; }
    public required string Color { get; set; }

    public ICollection<EventCategory> Events { get; set; } = [];
}
