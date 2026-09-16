using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodigoActivo.API.Attributes;

/// <summary>
/// Applies allow only self validation or authorization to the annotated target.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowOnlySelfAttribute : Attribute, IAsyncAuthorizationFilter
{
    private const string RouteKey = "userId";

    /// <summary>
    /// Authorizes the current request against the attribute requirements.
    /// </summary>
    /// <param name="context">Database context used for persistence.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
        var isOwnChild = await users.ExistsAsync(
            u => u.Id == targetUserId && u.ParentId == currentUserId,
            context.HttpContext.RequestAborted
        );
        if (isOwnChild)
        {
            return;
        }

        context.Result = new ForbidResult();
    }
}
