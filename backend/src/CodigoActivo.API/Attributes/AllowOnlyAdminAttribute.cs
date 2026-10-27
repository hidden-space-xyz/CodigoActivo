using CodigoActivo.API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodigoActivo.API.Attributes;

/// <summary>
/// Restricts the annotated target to administrators: a request without a signed-in user is
/// challenged (401) and one from any other user is forbidden (403).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowOnlyAdminAttribute : AuthorizeAttribute, IAuthorizationFilter
{
    /// <summary>
    /// Role claim value that the session cookie of an administrator carries.
    /// </summary>
    public const string AdminRole = "admin";

    /// <summary>
    /// Initializes the attribute so it also requires the <see cref="AdminRole"/> role.
    /// </summary>
    public AllowOnlyAdminAttribute()
    {
        Roles = AdminRole;
    }

    /// <summary>
    /// Challenges a request without a signed-in user and forbids one from a non-administrator.
    /// </summary>
    /// <param name="context">Authorization context of the request; its result is set to refuse it.</param>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.GetUserId() is null)
        {
            context.Result = new ChallengeResult();
            return;
        }

        if (!user.IsAdmin())
        {
            context.Result = new ForbidResult();
        }
    }
}
