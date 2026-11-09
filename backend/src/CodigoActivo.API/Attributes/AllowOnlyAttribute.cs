using CodigoActivo.API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodigoActivo.API.Attributes;

/// <summary>
/// Base of the attributes that restrict the annotated target to some signed-in users: a request
/// without a signed-in user is challenged (401) and one <see cref="IsAllowedAsync"/> refuses is
/// forbidden (403).
/// </summary>
public abstract class AllowOnlyAttribute : AuthorizeAttribute, IAsyncAuthorizationFilter
{
    /// <summary>
    /// Challenges a request without a signed-in user and forbids one the attribute refuses.
    /// </summary>
    /// <param name="context">
    /// Authorization context of the request; its result is set to refuse it.
    /// </param>
    /// <returns>A task that completes once the request is checked.</returns>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.HttpContext.User.GetUserId() is null)
        {
            context.Result = new ChallengeResult();
            return;
        }

        if (!await IsAllowedAsync(context))
        {
            context.Result = new ForbidResult();
        }
    }

    /// <summary>
    /// Tells whether the signed-in user may make the request.
    /// </summary>
    /// <param name="context">Authorization context of a request with a signed-in user.</param>
    /// <returns><see langword="true"/> when the request may go on.</returns>
    protected abstract ValueTask<bool> IsAllowedAsync(AuthorizationFilterContext context);
}
