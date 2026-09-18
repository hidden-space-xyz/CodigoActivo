namespace CodigoActivo.API.Security;

/// <summary>
/// Defines how long an authenticated session lasts. The same value is the absolute expiry of the
/// session cookie and of the <c>user_sessions</c> row the cookie points at, so neither outlives the
/// other.
/// </summary>
public sealed class SessionLifetimeOptions
{
    /// <summary>
    /// Identifies the lifetime used when <c>Auth:ExpireHours</c> is absent or not a positive number.
    /// </summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(8);

    /// <summary>
    /// Gets or sets the absolute lifetime of a session.
    /// </summary>
    public TimeSpan Lifetime { get; set; } = DefaultLifetime;
}
