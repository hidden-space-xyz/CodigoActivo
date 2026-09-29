using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// Session opened after a successful second factor. One row exists per session cookie issued, so
/// a ticket can be revoked on the server instead of staying valid until the cookie expires.
/// </summary>
public class UserSession : IdentifiableEntity, IAggregateRoot
{
    private UserSession() { }

    /// <summary>
    /// Gets the identifier of the account the session belongs to.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when the session was opened.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when the session stops being accepted. It matches the absolute
    /// expiry of the cookie that carries the session identifier.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// Opens a session for an account.
    /// </summary>
    /// <param name="userId">Identifier of the account.</param>
    /// <param name="now">Current time.</param>
    /// <param name="lifetime">How long the session is accepted.</param>
    /// <returns>The new session.</returns>
    public static UserSession Start(Guid userId, DateTimeOffset now, TimeSpan lifetime)
    {
        return new UserSession
        {
            UserId = userId,
            CreatedAt = now,
            ExpiresAt = now + lifetime,
        };
    }
}
