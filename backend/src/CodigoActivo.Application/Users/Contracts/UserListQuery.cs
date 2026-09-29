using CodigoActivo.Application.Common.Querying;

namespace CodigoActivo.Application.Users.Contracts;

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
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the email value.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets text the phone or the secondary phone must contain.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Gets or sets text the stored DNI or NIE must contain.
    /// </summary>
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
    /// Gets or sets the birth date from value.
    /// </summary>
    public DateOnly? BirthDateFrom { get; set; }

    /// <summary>
    /// Gets or sets the birth date to value.
    /// </summary>
    public DateOnly? BirthDateTo { get; set; }
}
