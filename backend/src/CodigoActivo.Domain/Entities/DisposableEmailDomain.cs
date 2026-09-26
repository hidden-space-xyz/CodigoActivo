namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents one domain of the stored disposable email domain list. Addresses at the domain or at
/// any of its subdomains are refused when an account email is registered or changed. The rows are
/// replaced as a whole by the background refresh, so they always hold the last list that passed
/// validation, or nothing when no list was ever obtained.
/// </summary>
public class DisposableEmailDomain
{
    /// <summary>
    /// Gets or sets the domain name in lowercase ASCII, with internationalized names in their
    /// punycode form. It is also the key of the row.
    /// </summary>
    public string Domain { get; set; } = string.Empty;
}
