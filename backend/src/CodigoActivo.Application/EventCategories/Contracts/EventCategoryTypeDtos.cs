using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.EventCategories.Contracts;

/// <summary>
/// Contains the event category type data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Color">The color value.</param>
public record EventCategoryTypeResponse(Guid Id, string Name, string Color)
{
    /// <summary>
    /// Initializes an empty event category type response for serialization.
    /// </summary>
    public EventCategoryTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty) { }
}

/// <summary>
/// Contains the client-supplied data used to create an event category type.
/// </summary>
/// <param name="Name">The name value.</param>
/// <param name="Color">The color value.</param>
public record CreateEventCategoryTypeRequest(
    [Required] [MaxLength(120)] [NotBlank] string Name,
    [Required] [MaxLength(9)] [RegularExpression("^#[0-9A-Fa-f]{6}$")] string Color
);

/// <summary>
/// Contains the client-supplied data used to update the event category type.
/// </summary>
/// <param name="Name">The name value.</param>
/// <param name="Color">The color value.</param>
public record UpdateEventCategoryTypeRequest(
    [Required] [MaxLength(120)] [NotBlank] string Name,
    [Required] [MaxLength(9)] [RegularExpression("^#[0-9A-Fa-f]{6}$")] string Color
);
