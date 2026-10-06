using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.EventCategories.Commands;
using CodigoActivo.Domain.EventCategories;

namespace CodigoActivo.API.EventCategories.Contracts;

/// <summary>
/// Contains the client-supplied data used to create an event category type.
/// </summary>
/// <param name="Name">The name value.</param>
/// <param name="Color">The color value.</param>
public record CreateEventCategoryTypeRequest(
    [Required] [MaxLength(120)] string Name,
    [Required] [MaxLength(9)] [RegularExpression("^#[0-9A-Fa-f]{6}$")] string Color
)
{
    /// <summary>
    /// Builds the command that creates the category type.
    /// </summary>
    /// <returns>The command.</returns>
    public CreateEventCategoryTypeCommand ToCommand()
    {
        return new CreateEventCategoryTypeCommand(Name, Color);
    }
}

/// <summary>
/// Contains the client-supplied data used to update the event category type.
/// </summary>
/// <param name="Name">The name value.</param>
/// <param name="Color">The color value.</param>
public record UpdateEventCategoryTypeRequest(
    [Required] [MaxLength(120)] string Name,
    [Required] [MaxLength(9)] [RegularExpression("^#[0-9A-Fa-f]{6}$")] string Color
)
{
    /// <summary>
    /// Builds the command that updates the category type.
    /// </summary>
    /// <param name="categoryTypeId">Identifier of the category type.</param>
    /// <returns>The command.</returns>
    public UpdateEventCategoryTypeCommand ToCommand(EventCategoryTypeId categoryTypeId)
    {
        return new UpdateEventCategoryTypeCommand(categoryTypeId, Name, Color);
    }
}
