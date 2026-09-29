using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// The administrator account created on the first start. It always exists: it can be neither
/// deleted nor stripped of its administrator rights, and it inherits the content credited to the
/// accounts that are erased.
/// </summary>
public static class InitialAdministrator
{
    /// <summary>
    /// Gets the identifier of the initial administrator.
    /// </summary>
    public static Guid Id => SeedIds.Users.InitialAdministrator;

    /// <summary>
    /// Checks that an account may be deleted.
    /// </summary>
    /// <param name="userId">Identifier of the account.</param>
    /// <returns>Success, or forbidden for the initial administrator.</returns>
    public static Result EnsureMayBeDeleted(Guid userId)
    {
        return userId == Id
            ? Error.Forbidden(ErrorCode.UserDeleteInitialAdminForbidden)
            : Result.Success();
    }

    /// <summary>
    /// Checks that an account may lose its administrator rights.
    /// </summary>
    /// <param name="userId">Identifier of the account.</param>
    /// <returns>Success, or forbidden for the initial administrator.</returns>
    public static Result EnsureMayLoseAdminRights(Guid userId)
    {
        return userId == Id
            ? Error.Forbidden(ErrorCode.UserCannotRemoveInitialAdmin)
            : Result.Success();
    }
}
