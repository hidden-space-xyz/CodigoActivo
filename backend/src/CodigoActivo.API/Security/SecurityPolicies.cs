namespace CodigoActivo.API.Security;

/// <summary>
/// Names the rate-limit policies of the endpoints. Each one has its own per-minute budget in
/// <see cref="ApiRateLimitOptions"/> and its own process-wide cap on concurrent requests.
/// </summary>
public static class SecurityPolicies
{
    /// <summary>
    /// Identifies the policy of the endpoints that check a password, a one-time code or an emailed
    /// token, counted per IP whether or not the caller is signed in.
    /// </summary>
    public const string Credentials = "credentials";

    /// <summary>
    /// Identifies the policy applied to database-intensive endpoints, such as reports, logs, the
    /// personal data copy and the blocked data consultations.
    /// </summary>
    public const string Reports = "reports";

    /// <summary>
    /// Identifies the policy applied to administrator email queued for a single recipient.
    /// </summary>
    public const string SingleRecipientEmail = "single-recipient-email";

    /// <summary>
    /// Identifies the policy applied to administrator email queued for many recipients.
    /// </summary>
    public const string BulkEmail = "bulk-email";

    /// <summary>
    /// Identifies the policy applied to multipart file uploads.
    /// </summary>
    public const string FileUploads = "file-uploads";
}
