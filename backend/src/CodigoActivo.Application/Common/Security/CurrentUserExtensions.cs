using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Common.Security;

/// <summary>
/// Reads the person a use case runs for.
/// </summary>
public static class CurrentUserExtensions
{
    /// <summary>
    /// Gets the identifier of the signed-in user of a use case that only a signed-in user can start.
    /// </summary>
    /// <param name="currentUser">Person the use case runs for.</param>
    /// <returns>The identifier of the signed-in user.</returns>
    /// <exception cref="InvalidOperationException">Nobody is signed in.</exception>
    public static UserId RequiredId(this ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        return currentUser.Id
            ?? throw new InvalidOperationException("This use case needs a signed-in user.");
    }
}
