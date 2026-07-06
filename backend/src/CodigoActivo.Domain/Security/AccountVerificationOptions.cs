namespace CodigoActivo.Domain.Security;

public sealed class AccountVerificationOptions
{
    public static readonly TimeSpan DefaultOtpLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan DefaultResendCooldown = TimeSpan.FromSeconds(60);

    public bool Required { get; set; } = true;

    public TimeSpan OtpLifetime { get; set; } = DefaultOtpLifetime;

    public TimeSpan ResendCooldown { get; set; } = DefaultResendCooldown;
}
