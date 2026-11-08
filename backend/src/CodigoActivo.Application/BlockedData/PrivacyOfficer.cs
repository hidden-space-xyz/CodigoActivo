using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.BlockedData;

/// <summary>
/// Decides who may consult the blocked data: the active administrator the initial administrator
/// designated for data protection, whatever its second factor.
/// </summary>
/// <param name="users">Repository used to load the signed-in account.</param>
/// <param name="currentUser">Signed-in user.</param>
public sealed class PrivacyOfficer(IUserRepository users, ICurrentUser currentUser)
{
    /// <summary>
    /// Tells whether an account is the designated one.
    /// </summary>
    /// <param name="isPrivacyOfficer">
    /// Whether the account is designated for data protection.
    /// </param>
    /// <param name="isActive">Whether the account is active.</param>
    /// <returns><see langword="true"/> when it is the designated account.</returns>
    public static bool Designates(bool isPrivacyOfficer, bool isActive)
    {
        return isPrivacyOfficer && isActive;
    }

    /// <summary>
    /// Loads the signed-in account when it may consult the blocked data.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is the account, or
    /// <see cref="ApplicationErrorCode.BlockedDataOfficerOnly"/> for any other account.
    /// </returns>
    public async Task<Result<User>> SignedInOfficerAsync(CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(currentUser.RequiredId(), ct);
        if (user is null || !Designates(user.IsPrivacyOfficer, user.Status is UserStatus.Active))
        {
            return Error.Forbidden(ApplicationErrorCode.BlockedDataOfficerOnly);
        }

        return user;
    }
}
