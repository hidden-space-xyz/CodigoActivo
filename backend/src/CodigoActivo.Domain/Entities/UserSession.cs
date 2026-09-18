using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted user session domain entity and its relationships. One row exists per
/// session cookie issued after a successful second factor, so a ticket can be revoked on the server
/// instead of staying valid until the cookie expires.
/// </summary>
public class UserSession : IdentifiableEntity
{
    /// <summary>
    /// Gets or sets the identifier of the associated user.
    /// </summary>
    public Guid UserId { get; set; }
    /// <summary>
    /// Gets or sets the associated user.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Gets or sets the UTC timestamp when the session was opened.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the session stops being accepted. It matches the
    /// absolute expiry of the cookie that carries the session identifier.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
