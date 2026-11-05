using CodigoActivo.API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodigoActivo.API.Attributes;

/// <summary>
/// Restricts the annotated target to the initial administrator, the superadministrator, and, with
/// <see cref="PrivacyOfficerAllowed"/>, to the account designated for data protection: a request
/// without a signed-in user is challenged (401) and one from any other user, administrators
/// included, is forbidden (403).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowOnlySuperAdministratorAttribute : AuthorizeAttribute, IAuthorizationFilter
{
    /// <summary>
    /// Gets or sets whether the administrator designated for data protection passes too. Defaults
    /// to <see langword="false"/>.
    /// </summary>
    public bool PrivacyOfficerAllowed { get; set; }

    /// <summary>
    /// Challenges a request without a signed-in user and forbids one from any other account.
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

        if (!user.IsSuperAdministrator() && !(PrivacyOfficerAllowed && user.IsPrivacyOfficer()))
        {
            context.Result = new ForbidResult();
        }
    }
}
