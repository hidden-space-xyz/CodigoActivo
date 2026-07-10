namespace CodigoActivo.Domain.Security;

public sealed class PasswordResetOptions
{
    public static readonly TimeSpan DefaultCodeLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan DefaultResendCooldown = TimeSpan.FromSeconds(60);

    public TimeSpan CodeLifetime { get; set; } = DefaultCodeLifetime;

    public TimeSpan ResendCooldown { get; set; } = DefaultResendCooldown;
}
