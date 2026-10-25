using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.BlockedData;

/// <summary>
/// Decides who may consult the blocked data: the active administrator whose email is the one
/// <see cref="PrivacyOfficerOptions"/> designates, and only while they use an authenticator app,
/// since with emailed codes whoever reads the mailbox would hold both factors.
/// </summary>
/// <param name="options">Designation of the person responsible for data protection.</param>
/// <param name="users">Repository used to load the signed-in account.</param>
/// <param name="currentUser">Signed-in user.</param>
public sealed class PrivacyOfficer(
    PrivacyOfficerOptions options,
    IUserRepository users,
    ICurrentUser currentUser
)
{
    /// <summary>
    /// Tells whether an account is the designated one.
    /// </summary>
    /// <param name="isAdmin">Whether the account is an administrator.</param>
    /// <param name="isActive">Whether the account is active.</param>
    /// <param name="email">Email of the account.</param>
    /// <returns><see langword="true"/> when it is the designated account.</returns>
    public bool Designates(bool isAdmin, bool isActive, string? email)
    {
        return isAdmin && isActive && string.Equals(email, options.Email, StringComparison.Ordinal);
    }

    /// <summary>
    /// Loads the signed-in account when it may consult the blocked data.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is the account, <see cref="ApplicationErrorCode.BlockedDataOfficerOnly"/>
    /// for any other account, or <see cref="ApplicationErrorCode.BlockedDataAuthenticatorRequired"/>
    /// while it has no authenticator app.
    /// </returns>
    public async Task<Result<User>> SignedInOfficerAsync(CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(currentUser.RequiredId(), ct);
        if (
            user is null
            || !Designates(user.IsAdmin, user.Status is UserStatus.Active, user.Email?.Value)
        )
        {
            return Error.Forbidden(ApplicationErrorCode.BlockedDataOfficerOnly);
        }

        return user.TwoFactorMethod is TwoFactorMethod.Authenticator
            ? user
            : Error.Forbidden(ApplicationErrorCode.BlockedDataAuthenticatorRequired);
    }
}
