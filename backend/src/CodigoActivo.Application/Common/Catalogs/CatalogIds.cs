using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Common.Catalogs;

/// <summary>
/// Stable identifiers of the closed catalogs. The domain works with their enumerations; the
/// reference tables, which keep the name, description and color shown to people, the foreign keys
/// and the API use these identifiers.
/// </summary>
public static class CatalogIds
{
    /// <summary>
    /// Gets the identifiers of <see cref="UserStatus"/>.
    /// </summary>
    public static CatalogKeys<UserStatus> UserStatuses { get; } =
        new(
            new Dictionary<UserStatus, Guid>
            {
                [UserStatus.Pending] = new("086e64b7-79e4-4b2d-a6c9-f69ff8243df1"),
                [UserStatus.Active] = new("766f114c-6168-4be5-89f2-bae2a7a919e4"),
                [UserStatus.Blocked] = new("37e9d1e6-1cf3-4c13-a1d8-41b86986d282"),
                [UserStatus.Dependent] = new("45a26e12-404b-43b8-b7a7-cdf5f0fc1c4d"),
            }
        );

    /// <summary>
    /// Gets the identifiers of <see cref="UserType"/>.
    /// </summary>
    public static CatalogKeys<UserType> UserTypes { get; } =
        new(
            new Dictionary<UserType, Guid>
            {
                [UserType.Member] = new("b0df7ac6-1312-412f-9c2a-88e6cdfb6e1c"),
                [UserType.Sponsor] = new("8e0b7dc4-59d3-4c3b-9a71-4f25c6b0de88"),
                [UserType.Participant] = new("1c038ae8-306f-4785-a5f5-b9c25e5cc4aa"),
            }
        );

    /// <summary>
    /// Gets the identifiers of <see cref="ActivityRole"/>.
    /// </summary>
    public static CatalogKeys<ActivityRole> ActivityRoles { get; } =
        new(
            new Dictionary<ActivityRole, Guid>
            {
                [ActivityRole.Leader] = new("5bd627de-831a-4169-874e-26a90550db9f"),
                [ActivityRole.Volunteer] = new("3b31564d-9879-434c-9152-5907db0c46fb"),
                [ActivityRole.Participant] = new("03a5613e-a2d2-42da-94d2-b6d63c0f01b5"),
            }
        );

    /// <summary>
    /// Gets the identifiers of <see cref="AssignmentStatus"/>.
    /// </summary>
    public static CatalogKeys<AssignmentStatus> AssignmentStatuses { get; } =
        new(
            new Dictionary<AssignmentStatus, Guid>
            {
                [AssignmentStatus.Requested] = new("3d717eeb-de06-44b8-b7df-2cc3e2ce5cb0"),
                [AssignmentStatus.Confirmed] = new("3c172c13-d238-4f0b-a61b-0a5ffc6a53ba"),
                [AssignmentStatus.Denied] = new("714c9041-5536-420a-8176-bf745957d80e"),
            }
        );

    /// <summary>
    /// Gets the identifiers of <see cref="ActivityModality"/>.
    /// </summary>
    public static CatalogKeys<ActivityModality> ActivityModalities { get; } =
        new(
            new Dictionary<ActivityModality, Guid>
            {
                [ActivityModality.Presencial] = new("3a7956c5-2346-4fc3-b3e8-10ecc07f1e1f"),
                [ActivityModality.Online] = new("44b62b00-17f5-46c3-ac65-245fbd8e7db2"),
            }
        );

    /// <summary>
    /// Gets the identifiers of <see cref="ResourceType"/>.
    /// </summary>
    public static CatalogKeys<ResourceType> ResourceTypes { get; } =
        new(
            new Dictionary<ResourceType, Guid>
            {
                [ResourceType.Internal] = new("47833436-b131-4536-87a6-737a9680a423"),
                [ResourceType.External] = new("d4b28595-eed5-4728-ad87-8eaf9d8ce754"),
            }
        );
}
