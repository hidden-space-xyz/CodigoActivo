namespace CodigoActivo.Domain.Events;

/// <summary>
/// Category that tags an event; part of the event aggregate.
/// </summary>
public class EventCategory
{
    private EventCategory() { }

    internal EventCategory(Guid eventId, Guid eventCategoryTypeId)
    {
        EventId = eventId;
        EventCategoryTypeId = eventCategoryTypeId;
    }

    /// <summary>
    /// Gets the identifier of the event.
    /// </summary>
    public Guid EventId { get; private set; }

    /// <summary>
    /// Gets the identifier of the category.
    /// </summary>
    public Guid EventCategoryTypeId { get; private set; }
}
