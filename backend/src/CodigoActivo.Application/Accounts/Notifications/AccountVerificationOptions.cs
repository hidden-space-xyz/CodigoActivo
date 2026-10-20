namespace CodigoActivo.Application.Accounts.Notifications;

/// <summary>
/// Defines configuration values for account verification.
/// </summary>
public sealed class AccountVerificationOptions
{
    /// <summary>
    /// Gets or sets the otp lifetime value.
    /// </summary>
    public TimeSpan OtpLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets the resend cooldown value.
    /// </summary>
    public TimeSpan ResendCooldown { get; set; } = TimeSpan.FromSeconds(60);
}
