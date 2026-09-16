namespace CodigoActivo.API.Security;

/// <summary>
/// Defines the shared security policies used for cache and authorization configuration.
/// </summary>
public static class SecurityPolicies
{
    /// <summary>
    /// Identifies the credentials configuration or policy value.
    /// </summary>
    public const string Credentials = "credentials";

    /// <summary>
    /// Identifies the policy applied to database-intensive report endpoints.
    /// </summary>
    public const string Reports = "reports";

    /// <summary>
    /// Identifies the policy applied to synchronous single-recipient administrator email.
    /// </summary>
    public const string SingleRecipientEmail = "single-recipient-email";

    /// <summary>
    /// Identifies the policy applied to synchronous bulk administrator email.
    /// </summary>
    public const string BulkEmail = "bulk-email";

    /// <summary>
    /// Identifies the policy applied to multipart file creation and replacement.
    /// </summary>
    public const string FileUploads = "file-uploads";
}
