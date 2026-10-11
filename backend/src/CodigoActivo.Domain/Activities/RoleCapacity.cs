namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Desired number of people for one role.
/// </summary>
/// <param name="Role">Role.</param>
/// <param name="DesiredCount">
/// Desired number of people, from <see cref="MinDesiredCount"/> to <see cref="MaxDesiredCount"/>.
/// </param>
public sealed record RoleCapacity(ActivityRole Role, int DesiredCount)
{
    /// <summary>
    /// Fewest people a role can ask for.
    /// </summary>
    public const int MinDesiredCount = 1;

    /// <summary>
    /// Most people a role can ask for.
    /// </summary>
    public const int MaxDesiredCount = 10000;
}
