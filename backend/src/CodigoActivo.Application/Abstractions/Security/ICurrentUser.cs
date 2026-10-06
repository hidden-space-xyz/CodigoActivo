using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Abstractions.Security;

/// <summary>
/// Person on whose behalf a use case runs: the signed-in user of the request, or nobody for work
/// that no one started, such as scheduled cleanups.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// Gets the identifier of the signed-in user, or <see langword="null"/> when nobody is.
    /// </summary>
    public UserId? Id { get; }

    /// <summary>
    /// Gets a value indicating whether the signed-in user is an administrator.
    /// </summary>
    public bool IsAdmin { get; }
}
