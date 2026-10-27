namespace CodigoActivo.Application.Accounts.Notifications;

/// <summary>
/// Defines configuration values for account verification.
/// </summary>
public sealed class AccountVerificationOptions
{
    /// <summary>
    /// Gets or sets how long an emailed verification code stays valid; the code that confirms a new
    /// email address shares it.
    /// </summary>
    public TimeSpan OtpLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets the minimum time between two verification emails for the same account.
    /// </summary>
    public TimeSpan ResendCooldown { get; set; } = TimeSpan.FromSeconds(60);
}
