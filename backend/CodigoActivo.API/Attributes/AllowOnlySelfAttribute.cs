using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodigoActivo.API.Attributes;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = false,
    Inherited = true
)]
public sealed class AllowOnlySelfAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string RouteKey { get; set; } = "userId";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var services = context.HttpContext.RequestServices;
        var user = context.HttpContext.User;

        if (user.GetUserId() is not { } currentUserId)
        {
            context.Result = new ChallengeResult();
            return;
        }

        if (user.IsAdmin())
        {
            return;
        }

        if (
            !context.RouteData.Values.TryGetValue(RouteKey, out var raw)
            || !Guid.TryParse(raw?.ToString(), out var targetUserId)
        )
        {
            context.Result = new ForbidResult();
            return;
        }

        if (targetUserId == currentUserId)
        {
            return;
        }

        var users = services.GetRequiredService<IUserRepository>();
        var target = await users.FindAsync(
            u => u.Id == targetUserId,
            context.HttpContext.RequestAborted
        );
        if (target?.ParentId == currentUserId)
        {
            return;
        }

        context.Result = new ForbidResult();
    }
}
