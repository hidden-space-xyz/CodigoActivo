namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Desired number of people for one role.
/// </summary>
/// <param name="Role">Role.</param>
/// <param name="DesiredCount">Desired number of people.</param>
public sealed record RoleCapacity(ActivityRole Role, int DesiredCount);
