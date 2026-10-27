using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.Users.Contracts;

/// <summary>
/// Kind of account a user list is narrowed to.
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
/// Carries the filters, sort and page of a user list; the manual email commands and audience
/// queries reuse its filters and ignore the sort and page. Unset filters match everyone, and text
/// filters match a fragment, ignoring case and acute accents.
/// </summary>
public sealed class UserListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the identifier of the only user to match.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Gets or sets text the first name and last name, joined by a space, must contain.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets text the email must contain.
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
    /// Gets or sets the identifier of the membership type users must have.
    /// </summary>
    public Guid? UserTypeId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the account status users must have.
    /// </summary>
    public Guid? UserStatusTypeId { get; set; }

    /// <summary>
    /// Gets or sets whether users must have, or lack, administrator rights.
    /// </summary>
    public bool? IsAdmin { get; set; }

    /// <summary>
    /// Gets or sets whether users must have given, or withheld, news consent.
    /// </summary>
    public bool? NewsConsent { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the guardian whose dependents are listed.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Gets or sets whether only independent accounts or only dependents are listed.
    /// </summary>
    [EnumDataType(typeof(UserScope))]
    public UserScope? Scope { get; set; }

    /// <summary>
    /// Gets or sets the earliest birth date, inclusive; set, it leaves out the users without one, the
    /// independent accounts.
    /// </summary>
    public DateOnly? BirthDateFrom { get; set; }

    /// <summary>
    /// Gets or sets the latest birth date, inclusive; set, it leaves out the users without one, the
    /// independent accounts.
    /// </summary>
    public DateOnly? BirthDateTo { get; set; }
}
