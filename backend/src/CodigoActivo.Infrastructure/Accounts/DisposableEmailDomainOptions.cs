namespace CodigoActivo.Infrastructure.Accounts;

/// <summary>
/// Defines configuration values for keeping the disposable email domain list up to date.
/// </summary>
public sealed class DisposableEmailDomainOptions
{
    /// <summary>
    /// Gets or sets the HTTPS address the list is downloaded from.
    /// </summary>
    public Uri SourceUrl { get; set; } =
        new(
            "https://raw.githubusercontent.com/disposable-email-domains/disposable-email-domains/main/disposable_email_blocklist.conf"
        );

    /// <summary>
    /// Gets or sets the wait after a successful refresh before the next one.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Gets or sets the wait after a failed refresh before trying again.
    /// </summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the delay applied before the first refresh, so startup work finishes first.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the longest time one download may take, headers and body included.
    /// </summary>
    public TimeSpan DownloadTimeout { get; set; } = TimeSpan.FromMinutes(1);
}
