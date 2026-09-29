namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Desired number of people for one role.
/// </summary>
/// <param name="ActivityRoleTypeId">Identifier of the role.</param>
/// <param name="DesiredCount">Desired number of people.</param>
public sealed record RoleCapacity(Guid ActivityRoleTypeId, int DesiredCount);
