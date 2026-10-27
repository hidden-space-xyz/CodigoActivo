namespace CodigoActivo.Application.Accounts.Sessions;

/// <summary>
/// Defines how long an authenticated session lasts. The same value sets the expiry of the persistent
/// session cookie and of the <c>user_sessions</c> row the cookie points at, but a claims refresh
/// re-issues the cookie with a later expiry, so the row is the absolute expiry that decides access
/// and a surviving cookie is rejected once its row is gone.
/// </summary>
public sealed class SessionLifetimeOptions
{
    /// <summary>
    /// Identifies the session lifetime, fixed in code: no setting can change it.
    /// </summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets or sets the absolute lifetime of a session.
    /// </summary>
    public TimeSpan Lifetime { get; set; } = DefaultLifetime;
}
