using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;

namespace CodigoActivo.Application.EventCategories.Commands;

/// <summary>
/// Carries the input required to update the event category type.
/// </summary>
/// <param name="CategoryTypeId">Identifier of the category type.</param>
/// <param name="Name">Name of up to 120 characters, unique among category types.</param>
/// <param name="Color">Color as <c>#RRGGBB</c>.</param>
public sealed record UpdateEventCategoryTypeCommand(
    EventCategoryTypeId CategoryTypeId,
    [property: Required, MaxLength(120), NotBlank] string Name,
    [property: Required, MaxLength(9), RegularExpression("^#[0-9A-Fa-f]{6}$")] string Color
) : ICommand<Result>;

/// <summary>
/// Executes the command to update the event category type.
/// </summary>
/// <param name="categoryTypes">Repository used to persist and retrieve category types.</param>
public sealed class UpdateEventCategoryTypeCommandHandler(
    IEventCategoryTypeRepository categoryTypes
) : ICommandHandler<UpdateEventCategoryTypeCommand, Result>
{
    /// <summary>
    /// Handles the request to update the event category type.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success;
    /// <see cref="ApplicationErrorCode.EventCategoryTypeNotFound"/>, or
    /// <see cref="ApplicationErrorCode.EventCategoryTypeNameAlreadyExists"/>.
    /// </returns>
    public async Task<Result> HandleAsync(
        UpdateEventCategoryTypeCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var categoryType = await categoryTypes.GetByIdAsync(command.CategoryTypeId, ct);
        if (categoryType is null)
        {
            return Error.NotFound(ApplicationErrorCode.EventCategoryTypeNotFound);
        }

        var name = command.Name.Trim();
        if (await categoryTypes.NameExistsAsync(name, command.CategoryTypeId, ct))
        {
            return Error.Conflict(ApplicationErrorCode.EventCategoryTypeNameAlreadyExists);
        }

        categoryType.Rename(name, command.Color);
        return Result.Success();
    }
}
