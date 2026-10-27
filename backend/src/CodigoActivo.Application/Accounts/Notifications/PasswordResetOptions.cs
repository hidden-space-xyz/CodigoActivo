namespace CodigoActivo.Application.Accounts.Notifications;

/// <summary>
/// Defines configuration values for password reset.
/// </summary>
public sealed class PasswordResetOptions
{
    /// <summary>
    /// Gets or sets how long an emailed password reset code stays valid.
    /// </summary>
    public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets the minimum time between two password reset emails for the same account.
    /// </summary>
    public TimeSpan ResendCooldown { get; set; } = TimeSpan.FromSeconds(60);
}
