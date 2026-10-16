using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.Users.Contracts;

/// <summary>
/// Identifies the supported user scope values.
/// </summary>
public enum UserScope
{
    /// <summary>
    /// Selects the independent accounts.
    /// </summary>
    Independent,

    /// <summary>
    /// Selects the dependents.
    /// </summary>
    Dependent,
}

/// <summary>
/// Carries the criteria used to user list.
/// </summary>
public sealed class UserListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Gets or sets the human-readable name.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the email value.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets text the phone or the secondary phone must contain.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Phone { get; set; }

    /// <summary>
    /// Gets or sets text the stored DNI or NIE must contain.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? NationalId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated user type.
    /// </summary>
    public Guid? UserTypeId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated user status type.
    /// </summary>
    public Guid? UserStatusTypeId { get; set; }

    /// <summary>
    /// Gets or sets the is admin value.
    /// </summary>
    public bool? IsAdmin { get; set; }

    /// <summary>
    /// Gets or sets whether users must have given, or withheld, promotional consent.
    /// </summary>
    public bool? PromotionalConsent { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated parent.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Gets or sets whether only independent accounts or only dependents are listed.
    /// </summary>
    [EnumDataType(typeof(UserScope))]
    public UserScope? Scope { get; set; }

    /// <summary>
    /// Gets or sets the birth date from value.
    /// </summary>
    public DateOnly? BirthDateFrom { get; set; }

    /// <summary>
    /// Gets or sets the birth date to value.
    /// </summary>
    public DateOnly? BirthDateTo { get; set; }
}
