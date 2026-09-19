namespace CodigoActivo.Application.Options;

/// <summary>
/// Defines configuration values for account verification.
/// </summary>
public sealed class AccountVerificationOptions
{
    /// <summary>
    /// Stores the shared default otp lifetime value.
    /// </summary>
    public static readonly TimeSpan DefaultOtpLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Stores the shared default resend cooldown value.
    /// </summary>
    public static readonly TimeSpan DefaultResendCooldown = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets the otp lifetime value.
    /// </summary>
    public TimeSpan OtpLifetime { get; set; } = DefaultOtpLifetime;

    /// <summary>
    /// Gets or sets the resend cooldown value.
    /// </summary>
    public TimeSpan ResendCooldown { get; set; } = DefaultResendCooldown;
}
