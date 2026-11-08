using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodigoActivo.API.Attributes;

/// <summary>
/// Restricts the annotated target to the accounts that hold a staff role or one that includes it:
/// a request without a signed-in user is challenged (401) and one from any other user is forbidden
/// (403).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowOnlyStaffAttribute : AuthorizeAttribute, IAuthorizationFilter
{
    /// <summary>
    /// Initializes the attribute so it also requires the role claim of the staff role.
    /// </summary>
    /// <param name="staffRole">
    /// Staff role required, <see cref="StaffRole.Editor"/> or higher.
    /// </param>
    public AllowOnlyStaffAttribute(StaffRole staffRole)
    {
        StaffRole = staffRole;
        Roles = ClaimsPrincipalExtensions.RoleOf(staffRole);
    }

    /// <summary>
    /// Gets the staff role required.
    /// </summary>
    public StaffRole StaffRole { get; }

    /// <summary>
    /// Challenges a request without a signed-in user and forbids one from any other account.
    /// </summary>
    /// <param name="context">
    /// Authorization context of the request; its result is set to refuse it.
    /// </param>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.GetUserId() is null)
        {
            context.Result = new ChallengeResult();
            return;
        }

        if (!user.HasStaffRole(StaffRole))
        {
            context.Result = new ForbidResult();
        }
    }
}
