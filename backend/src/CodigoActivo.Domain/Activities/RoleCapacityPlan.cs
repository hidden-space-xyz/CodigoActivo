using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Desired number of people per role of an activity, with each role at most once.
/// </summary>
public sealed class RoleCapacityPlan
{
    private RoleCapacityPlan(IReadOnlyList<RoleCapacity> items)
    {
        Items = items;
    }

    /// <summary>
    /// Gets the plan with no roles.
    /// </summary>
    public static RoleCapacityPlan None { get; } = new([]);

    /// <summary>
    /// Gets the desired number of people per role.
    /// </summary>
    public IReadOnlyList<RoleCapacity> Items { get; }

    /// <summary>
    /// Builds the plan from the capacities supplied.
    /// </summary>
    /// <param name="items">Capacities; <see langword="null"/> means none.</param>
    /// <returns>The plan, or a validation error when a role appears twice.</returns>
    public static Result<RoleCapacityPlan> Create(IReadOnlyList<RoleCapacity>? items)
    {
        if (items is null || items.Count is 0)
        {
            return None;
        }

        return items.Select(item => item.Role).Distinct().Count() != items.Count
            ? Error.Validation(DomainErrorCode.ActivityRoleCapacityDuplicated)
            : new RoleCapacityPlan([.. items]);
    }
}
