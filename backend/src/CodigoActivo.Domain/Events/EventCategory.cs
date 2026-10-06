using CodigoActivo.Domain.EventCategories;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Category that tags an event; part of the event aggregate.
/// </summary>
public class EventCategory
{
    private EventCategory() { }

    internal EventCategory(EventId eventId, EventCategoryTypeId eventCategoryTypeId)
    {
        EventId = eventId;
        EventCategoryTypeId = eventCategoryTypeId;
    }

    /// <summary>
    /// Gets the identifier of the event.
    /// </summary>
    public EventId EventId { get; private set; }

    /// <summary>
    /// Gets the identifier of the category.
    /// </summary>
    public EventCategoryTypeId EventCategoryTypeId { get; private set; }
}
