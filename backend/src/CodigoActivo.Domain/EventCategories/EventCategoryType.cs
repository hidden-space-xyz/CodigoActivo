using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.EventCategories;

/// <summary>
/// Category that tags events, shown with its own color. Its name is unique across categories.
/// </summary>
public class EventCategoryType : AggregateRoot<EventCategoryTypeId>
{
    private EventCategoryType() { }

    /// <summary>
    /// Gets the human-readable name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the display color.
    /// </summary>
    public string Color { get; private set; } = string.Empty;

    /// <summary>
    /// Creates a category.
    /// </summary>
    /// <param name="name">Name; surrounding spaces are removed.</param>
    /// <param name="color">Display color; surrounding spaces are removed.</param>
    /// <param name="id">Stable identifier for seeded categories; a new one otherwise.</param>
    /// <returns>The new category.</returns>
    public static EventCategoryType Create(
        string name,
        string color,
        EventCategoryTypeId? id = null
    )
    {
        var categoryType = new EventCategoryType { Id = id ?? EventCategoryTypeId.New() };
        categoryType.Apply(name, color);
        categoryType.Raise(new EventCategoryTypeCreated(categoryType.Id));
        return categoryType;
    }

    /// <summary>
    /// Replaces the name and color of the category.
    /// </summary>
    /// <param name="name">New name; surrounding spaces are removed.</param>
    /// <param name="color">New display color; surrounding spaces are removed.</param>
    public void Rename(string name, string color)
    {
        Apply(name, color);
        Raise(new EventCategoryTypeRenamed(Id));
    }

    /// <summary>
    /// Marks the category type as deleted.
    /// </summary>
    public void Delete()
    {
        Raise(new EventCategoryTypeDeleted(Id));
    }

    private void Apply(string name, string color)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(color);

        Name = name.Trim();
        Color = color.Trim();
    }
}
