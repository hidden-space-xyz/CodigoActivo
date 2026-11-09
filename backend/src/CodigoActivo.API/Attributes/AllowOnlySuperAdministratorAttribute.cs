using CodigoActivo.API.Extensions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodigoActivo.API.Attributes;

/// <summary>
/// Restricts the annotated target to the initial administrator, the superadministrator, and, with
/// <see cref="PrivacyOfficerAllowed"/>, to the account designated for data protection: a request
/// without a signed-in user is challenged (401) and one from any other user, administrators
/// included, is forbidden (403).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowOnlySuperAdministratorAttribute : AllowOnlyAttribute
{
    /// <summary>
    /// Gets or sets a value indicating whether the administrator designated for data protection
    /// passes too. Defaults to <see langword="false"/>.
    /// </summary>
    public bool PrivacyOfficerAllowed { get; set; }

    /// <inheritdoc />
    protected override ValueTask<bool> IsAllowedAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        return ValueTask.FromResult(
            user.IsSuperAdministrator() || (PrivacyOfficerAllowed && user.IsPrivacyOfficer())
        );
    }
}
