namespace CodigoActivo.API.Security;

/// <summary>
/// Defines the request budgets enforced per client over a one-minute sliding window.
/// </summary>
public sealed class ApiRateLimitOptions
{
    /// <summary>
    /// Gets or sets the requests allowed per authenticated user across all API endpoints.
    /// </summary>
    public int AuthenticatedRequestsPerMinute { get; set; } = 300;

    /// <summary>
    /// Gets or sets the requests allowed per anonymous IP across all API endpoints.
    /// </summary>
    public int AnonymousRequestsPerMinutePerIp { get; set; } = 3_000;

    /// <summary>
    /// Gets or sets the credential requests allowed per IP.
    /// </summary>
    public int CredentialRequestsPerMinutePerIp { get; set; } = 120;

    /// <summary>
    /// Gets or sets the report requests allowed per user.
    /// </summary>
    public int ReportRequestsPerMinutePerUser { get; set; } = 30;

    /// <summary>
    /// Gets or sets the single-recipient email requests allowed per user.
    /// </summary>
    public int SingleRecipientEmailRequestsPerMinutePerUser { get; set; } = 30;

    /// <summary>
    /// Gets or sets the bulk email requests allowed per user.
    /// </summary>
    public int BulkEmailRequestsPerMinutePerUser { get; set; } = 5;

    /// <summary>
    /// Gets or sets the file upload requests allowed per user.
    /// </summary>
    public int FileUploadRequestsPerMinutePerUser { get; set; } = 30;
}
