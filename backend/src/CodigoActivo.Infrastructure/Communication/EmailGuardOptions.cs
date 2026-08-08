namespace CodigoActivo.Infrastructure.Communication;

public sealed class EmailGuardOptions
{
    public const int DefaultRecipientBurst = 20;
    public const int DefaultRecipientPerHour = 10;
    public const int DefaultRecipientPerDay = 50;
    public const int DefaultGlobalBurst = 1000;
    public const int DefaultGlobalPerHour = 1000;
    public const int DefaultGlobalCredentialReserve = 200;
    public const int DefaultMaxTrackedRecipients = 50_000;

    public static readonly TimeSpan DefaultSweepInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan DefaultAlertInterval = TimeSpan.FromMinutes(15);

    public int RecipientBurst { get; set; } = DefaultRecipientBurst;

    public int RecipientPerHour { get; set; } = DefaultRecipientPerHour;

    public int RecipientPerDay { get; set; } = DefaultRecipientPerDay;

    public int GlobalBurst { get; set; } = DefaultGlobalBurst;

    public int GlobalPerHour { get; set; } = DefaultGlobalPerHour;

    public int GlobalCredentialReserve { get; set; } = DefaultGlobalCredentialReserve;

    public int EffectiveCredentialReserve =>
        Math.Clamp(GlobalCredentialReserve, 0, Math.Max(GlobalBurst - 1, 0));

    public int MaxTrackedRecipients { get; set; } = DefaultMaxTrackedRecipients;

    public TimeSpan SweepInterval { get; set; } = DefaultSweepInterval;

    public TimeSpan AlertInterval { get; set; } = DefaultAlertInterval;
}
