namespace CodigoActivo.Application.EventCategories.Contracts;

/// <summary>
/// Contains the event category type data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the category type.</param>
/// <param name="Name">Name, unique among category types.</param>
/// <param name="Color">Display color as <c>#RRGGBB</c>.</param>
public record EventCategoryTypeResponse(Guid Id, string Name, string Color)
{
    /// <summary>
    /// Initializes an empty event category type response for serialization.
    /// </summary>
    public EventCategoryTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty) { }
}
