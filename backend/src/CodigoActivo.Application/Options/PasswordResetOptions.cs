namespace CodigoActivo.Application.Options;

/// <summary>
/// Defines configuration values for password reset.
/// </summary>
public sealed class PasswordResetOptions
{
    /// <summary>
    /// Stores the shared default code lifetime value.
    /// </summary>
    public static readonly TimeSpan DefaultCodeLifetime = TimeSpan.FromMinutes(15);
    /// <summary>
    /// Stores the shared default resend cooldown value.
    /// </summary>
    public static readonly TimeSpan DefaultResendCooldown = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets the code lifetime value.
    /// </summary>
    public TimeSpan CodeLifetime { get; set; } = DefaultCodeLifetime;

    /// <summary>
    /// Gets or sets the resend cooldown value.
    /// </summary>
    public TimeSpan ResendCooldown { get; set; } = DefaultResendCooldown;
}
