using CodigoActivo.API.Extensions;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodigoActivo.API.Attributes;

/// <summary>
/// Refuses early a request on a user the signed-in user may not act for: administrators pass, and
/// anyone else needs <see cref="ActingUserPolicy"/> to accept the user named by the route.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowOnlySelfAttribute : Attribute, IAsyncAuthorizationFilter
{
    private const string RouteKey = "userId";

    /// <summary>
    /// Authorizes the current request against the attribute requirements.
    /// </summary>
    /// <param name="context">Authorization context of the request.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.GetUserId() is null)
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

        var allowed = await context
            .HttpContext.RequestServices.GetRequiredService<ActingUserPolicy>()
            .EnsureMayActForAsync(UserId.From(targetUserId), context.HttpContext.RequestAborted);
        if (allowed.IsFailure)
        {
            context.Result = new ForbidResult();
        }
    }
}
