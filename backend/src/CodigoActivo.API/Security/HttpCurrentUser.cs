using CodigoActivo.API.Extensions;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.API.Security;

/// <summary>
/// Signed-in user of the current HTTP request, read from its authenticated claims. Outside a
/// request, as in background work, nobody is signed in.
/// </summary>
/// <param name="accessor">Accessor of the current HTTP context.</param>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    /// <inheritdoc />
    public UserId? Id => accessor.HttpContext?.User.GetUserId() is { } id ? UserId.From(id) : null;

    /// <inheritdoc />
    public bool IsAdmin => accessor.HttpContext?.User.IsAdmin() is true;
}
