using CodigoActivo.API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodigoActivo.API.Attributes;

/// <summary>
/// Applies allow only admin validation or authorization to the annotated target.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowOnlyAdminAttribute : AuthorizeAttribute, IAuthorizationFilter
{
    /// <summary>
    /// Identifies the admin role configuration or policy value.
    /// </summary>
    public const string AdminRole = "admin";

    /// <summary>
    /// Initializes an allow only admin attribute with its required dependencies.
    /// </summary>
    public AllowOnlyAdminAttribute()
    {
        Roles = AdminRole;
    }

    /// <summary>
    /// Authorizes the current request against the attribute requirements.
    /// </summary>
    /// <param name="context">Database context used for persistence.</param>
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
