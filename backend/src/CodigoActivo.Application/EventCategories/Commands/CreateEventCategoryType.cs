using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;

namespace CodigoActivo.Application.EventCategories.Commands;

/// <summary>
/// Carries the input required to create an event category type.
/// </summary>
/// <param name="Name">Name, unique among category types.</param>
/// <param name="Color">Color as <c>#RRGGBB</c>.</param>
public sealed record CreateEventCategoryTypeCommand(
    [property: Required, MaxLength(120), NotBlank] string Name,
    [property: Required, MaxLength(9), RegularExpression("^#[0-9A-Fa-f]{6}$")] string Color
) : ICommand<Result<EventCategoryTypeId>>;

/// <summary>
/// Executes the command to create an event category type.
/// </summary>
/// <param name="categoryTypes">Repository used to persist and retrieve category types.</param>
public sealed class CreateEventCategoryTypeCommandHandler(
    IEventCategoryTypeRepository categoryTypes
) : ICommandHandler<CreateEventCategoryTypeCommand, Result<EventCategoryTypeId>>
{
    /// <summary>
    /// Handles the request to create an event category type.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifier of the created item, or an application error on failure.</returns>
    public async Task<Result<EventCategoryTypeId>> HandleAsync(
        CreateEventCategoryTypeCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var name = command.Name.Trim();
        if (await categoryTypes.NameExistsAsync(name, ct: ct))
        {
            return Error.Conflict(ApplicationErrorCode.EventCategoryTypeNameAlreadyExists);
        }

        var categoryType = EventCategoryType.Create(name, command.Color);
        await categoryTypes.AddAsync(categoryType, ct);
        return categoryType.Id;
    }
}
