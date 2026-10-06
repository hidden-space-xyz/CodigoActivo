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
    public static UserId Id { get; } =
        UserId.From(new Guid("e8a173b3-72b2-4e11-a35b-2f3810dfe259"));

    /// <summary>
    /// Checks that an account may be deleted.
    /// </summary>
    /// <param name="userId">Identifier of the account.</param>
    /// <returns>Success, or forbidden for the initial administrator.</returns>
    public static Result EnsureMayBeDeleted(UserId userId)
    {
        return userId == Id
            ? Error.Forbidden(DomainErrorCode.UserDeleteInitialAdminForbidden)
            : Result.Success();
    }

    /// <summary>
    /// Checks that an account may lose its administrator rights.
    /// </summary>
    /// <param name="userId">Identifier of the account.</param>
    /// <returns>Success, or forbidden for the initial administrator.</returns>
    public static Result EnsureMayLoseAdminRights(UserId userId)
    {
        return userId == Id
            ? Error.Forbidden(DomainErrorCode.UserCannotRemoveInitialAdmin)
            : Result.Success();
    }
}
