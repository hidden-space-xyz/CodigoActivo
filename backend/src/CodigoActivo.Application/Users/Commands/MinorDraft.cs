using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Details of a minor added to a household.
/// </summary>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="BirthDate">Date of birth; today at the latest.</param>
/// <param name="Gender">Gender.</param>
public sealed record MinorDraft(
    [property: Required, MaxLength(120), NotBlank] string FirstName,
    [property: Required, MaxLength(120), NotBlank] string LastName,
    [property: NotDefaultOrFutureDate] DateOnly BirthDate,
    [property: EnumDataType(typeof(Gender))] Gender Gender
)
{
    /// <summary>
    /// Gets the details the minor is created with.
    /// </summary>
    /// <returns>The details.</returns>
    public PersonDetails ToDetails()
    {
        return new PersonDetails(FirstName, LastName, Gender, BirthDate: BirthDate);
    }
}
