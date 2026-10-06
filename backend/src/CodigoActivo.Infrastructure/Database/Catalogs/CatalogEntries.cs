using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Infrastructure.Database.Catalogs;

/// <summary>
/// Reference row of <see cref="UserStatus"/>.
/// </summary>
public sealed class UserStatusEntry : CatalogEntry<UserStatus>
{
    /// <summary>
    /// Gets the detailed description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the color used to display the status.
    /// </summary>
    public string Color { get; init; } = string.Empty;
}

/// <summary>
/// Reference row of <see cref="UserType"/>.
/// </summary>
public sealed class UserTypeEntry : CatalogEntry<UserType>
{
    /// <summary>
    /// Gets the detailed description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the color used to display the type.
    /// </summary>
    public string Color { get; init; } = string.Empty;
}

/// <summary>
/// Reference row of <see cref="ActivityRole"/>.
/// </summary>
public sealed class ActivityRoleEntry : CatalogEntry<ActivityRole>
{
    /// <summary>
    /// Gets the detailed description.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Reference row of <see cref="AssignmentStatus"/>.
/// </summary>
public sealed class AssignmentStatusEntry : CatalogEntry<AssignmentStatus>
{
    /// <summary>
    /// Gets the detailed description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the color used to display the status.
    /// </summary>
    public string Color { get; init; } = string.Empty;
}

/// <summary>
/// Reference row of <see cref="ActivityModality"/>.
/// </summary>
public sealed class ActivityModalityEntry : CatalogEntry<ActivityModality>;

/// <summary>
/// Reference row of <see cref="ResourceType"/>.
/// </summary>
public sealed class ResourceTypeEntry : CatalogEntry<ResourceType>
{
    /// <summary>
    /// Gets the detailed description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the color used to display the type.
    /// </summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether resources of this type link to an external page.
    /// </summary>
    public bool IsExternal { get; init; }
}
