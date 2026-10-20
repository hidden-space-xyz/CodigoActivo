namespace CodigoActivo.Application.Accounts.Notifications;

/// <summary>
/// Defines configuration values for password reset.
/// </summary>
public sealed class PasswordResetOptions
{
    /// <summary>
    /// Gets or sets the code lifetime value.
    /// </summary>
    public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets the resend cooldown value.
    /// </summary>
    public TimeSpan ResendCooldown { get; set; } = TimeSpan.FromSeconds(60);
}
