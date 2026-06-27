using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class UserType : NamedEntity
{
    /// <summary>Hidden from the public frontend (e.g. the Admin role).</summary>
    public bool Hidden { get; set; }

    /// <summary>Selectable as a role for a minor during registration / from the account area.</summary>
    public bool IsAllowedForMinors { get; set; }

    /// <summary>Selectable as a role for an adult during registration / from the account area.</summary>
    public bool IsAllowedForAdults { get; set; }

    public ICollection<UserTypeAssignment> Assignments { get; set; } = [];
}
