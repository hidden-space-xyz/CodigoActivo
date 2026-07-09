namespace CodigoActivo.Domain.Entities;

public class EventCategory
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid EventCategoryTypeId { get; set; }
    public EventCategoryType EventCategoryType { get; set; } = null!;
}
