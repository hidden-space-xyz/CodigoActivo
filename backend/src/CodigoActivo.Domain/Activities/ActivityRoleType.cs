using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Role a person takes in an activity, from the fixed catalog of roles.
/// </summary>
public class ActivityRoleType : NamedEntity
{
    private ActivityRoleType() { }

    /// <summary>
    /// Creates an entry of the catalog.
    /// </summary>
    /// <param name="id">Known identifier of the role.</param>
    /// <param name="name">Human-readable name.</param>
    /// <param name="description">Detailed description.</param>
    /// <returns>The unsaved entry.</returns>
    public static ActivityRoleType Create(Guid id, string name, string description)
    {
        return new ActivityRoleType
        {
            Id = id,
            Name = name,
            Description = description,
        };
    }
}
