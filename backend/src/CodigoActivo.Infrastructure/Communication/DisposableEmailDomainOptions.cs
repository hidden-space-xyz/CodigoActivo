namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Defines configuration values for keeping the disposable email domain list up to date.
/// </summary>
public sealed class DisposableEmailDomainOptions
{
    /// <summary>
    /// Stores the shared default source url value: the blocklist published by the
    /// disposable-email-domains project on GitHub.
    /// </summary>
    public static readonly Uri DefaultSourceUrl = new(
        "https://raw.githubusercontent.com/disposable-email-domains/disposable-email-domains/main/disposable_email_blocklist.conf"
    );

    /// <summary>
    /// Stores the shared default refresh interval value.
    /// </summary>
    public static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromDays(1);

    /// <summary>
    /// Stores the shared default retry interval value.
    /// </summary>
    public static readonly TimeSpan DefaultRetryInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// Stores the shared default startup delay value.
    /// </summary>
    public static readonly TimeSpan DefaultStartupDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Stores the shared default download timeout value.
    /// </summary>
    public static readonly TimeSpan DefaultDownloadTimeout = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the HTTPS address the list is downloaded from.
    /// </summary>
    public Uri SourceUrl { get; set; } = DefaultSourceUrl;

    /// <summary>
    /// Gets or sets the wait after a successful refresh before the next one.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = DefaultRefreshInterval;

    /// <summary>
    /// Gets or sets the wait after a failed refresh before trying again.
    /// </summary>
    public TimeSpan RetryInterval { get; set; } = DefaultRetryInterval;

    /// <summary>
    /// Gets or sets the delay applied before the first refresh, so startup work finishes first.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = DefaultStartupDelay;

    /// <summary>
    /// Gets or sets the longest time one download may take, headers and body included.
    /// </summary>
    public TimeSpan DownloadTimeout { get; set; } = DefaultDownloadTimeout;
}
