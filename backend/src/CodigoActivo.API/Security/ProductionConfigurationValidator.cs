using System.Net;
using System.Net.Mail;

namespace CodigoActivo.API.Security;

public static class ProductionConfigurationValidator
{
    public static void Validate(IConfiguration config)
    {
        var errors = new List<string>();

        if (config.GetValue("DEMO_MODE", false))
        {
            errors.Add("DEMO_MODE must be false");
        }

        if (!config.GetValue("ACCOUNT_VERIFICATION_REQUIRED", true))
        {
            errors.Add("ACCOUNT_VERIFICATION_REQUIRED must be true");
        }

        var databasePassword = config["POSTGRES_PASSWORD"];
        if (string.IsNullOrEmpty(databasePassword) || databasePassword.Length < 16)
        {
            errors.Add("POSTGRES_PASSWORD must contain at least 16 characters");
        }

        var dataProtectionPassword = config["DATA_PROTECTION_CERTIFICATE_PASSWORD"];
        if (string.IsNullOrEmpty(dataProtectionPassword) || dataProtectionPassword.Length < 32)
        {
            errors.Add(
                "DATA_PROTECTION_CERTIFICATE_PASSWORD must contain at least 32 characters"
            );
        }

        var baseUrl = config["APP_BASE_URL"];
        if (
            !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
            || !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(baseUri.UserInfo)
            || !string.IsNullOrEmpty(baseUri.Query)
            || !string.IsNullOrEmpty(baseUri.Fragment)
            || baseUri.AbsolutePath != "/"
            || !IsPublicDomain(baseUri.IdnHost)
        )
        {
            errors.Add(
                "APP_BASE_URL must be the public HTTPS origin without a path, query or fragment"
            );
        }

        var sameSite = config["AUTH_SAMESITE"]?.Trim();
        if (
            !string.IsNullOrEmpty(sameSite)
            && !string.Equals(sameSite, "Lax", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sameSite, "Strict", StringComparison.OrdinalIgnoreCase)
        )
        {
            errors.Add("AUTH_SAMESITE must be Lax or Strict");
        }

        if (
            !int.TryParse(config["Auth:MaxConcurrentCredentialRequests"], out var credentialLimit)
            || credentialLimit is < 1 or > 32
        )
        {
            errors.Add("AUTH_MAX_CONCURRENT_CREDENTIAL_REQUESTS must be between 1 and 32");
        }

        if (
            !int.TryParse(config["Auth:MaxQueuedCredentialRequests"], out var credentialQueueLimit)
            || credentialQueueLimit is < 100 or > 1_000
        )
        {
            errors.Add("AUTH_MAX_QUEUED_CREDENTIAL_REQUESTS must be between 100 and 1000");
        }

        var smtpSecurity = config["SMTP_SECURITY"]?.Trim();
        if (
            !string.Equals(smtpSecurity, "StartTls", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(smtpSecurity, "SslOnConnect", StringComparison.OrdinalIgnoreCase)
        )
        {
            errors.Add("SMTP_SECURITY must be StartTls or SslOnConnect");
        }

        var smtpHost = config["SMTP_HOST"]?.Trim();
        if (
            string.IsNullOrEmpty(smtpHost)
            || Uri.CheckHostName(smtpHost) == UriHostNameType.Unknown
        )
        {
            errors.Add("SMTP_HOST must be a valid host name or IP address");
        }

        if (!int.TryParse(config["SMTP_PORT"], out var smtpPort) || smtpPort is < 1 or > 65_535)
        {
            errors.Add("SMTP_PORT must be between 1 and 65535");
        }

        var smtpFromAddress = config["SMTP_FROM_ADDRESS"]?.Trim();
        if (!IsSingleEmailAddress(smtpFromAddress))
        {
            errors.Add("SMTP_FROM_ADDRESS must be a single valid email address");
        }

        var smtpUsernameMissing = string.IsNullOrWhiteSpace(config["SMTP_USERNAME"]);
        var smtpPasswordMissing = string.IsNullOrWhiteSpace(config["SMTP_PASSWORD"]);
        if (smtpUsernameMissing != smtpPasswordMissing)
        {
            errors.Add("SMTP_USERNAME and SMTP_PASSWORD must either both be set or both be empty");
        }

        var bootstrapEmail = config["BOOTSTRAP_ADMIN_EMAIL"]?.Trim();
        if (!string.IsNullOrEmpty(bootstrapEmail) && !IsSingleEmailAddress(bootstrapEmail))
        {
            errors.Add("BOOTSTRAP_ADMIN_EMAIL must be a single valid email address");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Unsafe production configuration: " + string.Join("; ", errors)
            );
        }
    }

    private static bool IsSingleEmailAddress(string? value)
    {
        return !string.IsNullOrEmpty(value)
            && MailAddress.TryCreate(value, out var parsed)
            && string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublicDomain(string host)
    {
        if (IPAddress.TryParse(host, out _) || !host.Contains('.'))
        {
            return false;
        }

        string[] reservedSuffixes =
        [
            "example",
            "example.com",
            "example.net",
            "example.org",
            "invalid",
            "localhost",
            "test",
        ];
        return !reservedSuffixes.Any(suffix =>
            string.Equals(host, suffix, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith('.' + suffix, StringComparison.OrdinalIgnoreCase)
        );
    }
}
