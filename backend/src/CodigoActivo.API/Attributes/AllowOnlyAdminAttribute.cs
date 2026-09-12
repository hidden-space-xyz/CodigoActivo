using CodigoActivo.API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodigoActivo.API.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowOnlyAdminAttribute : AuthorizeAttribute, IAuthorizationFilter
{
    public const string AdminRole = "admin";

    public AllowOnlyAdminAttribute()
    {
        Roles = AdminRole;
    }

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
