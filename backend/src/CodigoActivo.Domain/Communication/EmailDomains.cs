using System.Globalization;

namespace CodigoActivo.Domain.Communication;

/// <summary>
/// Normalizes email domains and lists the names an address is looked up under. Domains are compared
/// in lowercase ASCII, with internationalized names in their punycode form and without a trailing
/// dot, and an address matches a listed domain when its own domain or any parent domain is listed.
/// The top-level domain alone is never a lookup name, so no entry can match a whole top-level
/// domain.
/// </summary>
public static class EmailDomains
{
    /// <summary>
    /// Normalizes a domain name for comparison.
    /// </summary>
    /// <param name="domain">Domain name to normalize.</param>
    /// <returns>
    /// The lowercase ASCII form without surrounding whitespace or trailing dots, or
    /// <see langword="null"/> when nothing remains. A name that is not a valid internationalized
    /// domain name is only lowercased.
    /// </returns>
    public static string? Normalize(string? domain)
    {
        var trimmed = domain?.Trim().TrimEnd('.');
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        try
        {
            return new IdnMapping().GetAscii(trimmed).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            return trimmed.ToLowerInvariant();
        }
    }

    /// <summary>
    /// Lists a normalized domain followed by each of its parent domains, stopping before the
    /// top-level domain: <c>a.mailinator.com</c> yields <c>a.mailinator.com</c> and
    /// <c>mailinator.com</c>.
    /// </summary>
    /// <param name="domain">Normalized domain name.</param>
    /// <returns>The names, empty when the domain has fewer than two labels or an empty label.</returns>
    public static IReadOnlyList<string> SelfAndParents(string domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        var labels = domain.Split('.');
        if (labels.Length < 2 || labels.Any(label => label.Length is 0))
        {
            return [];
        }

        return
        [
            .. Enumerable
                .Range(0, labels.Length - 1)
                .Select(start => string.Join('.', labels[start..])),
        ];
    }

    /// <summary>
    /// Lists the names an email address is looked up under: its normalized domain, taken after the
    /// last <c>@</c>, followed by its parent domains.
    /// </summary>
    /// <param name="email">Email address to inspect.</param>
    /// <returns>The names, empty when the address has no usable domain.</returns>
    public static IReadOnlyList<string> LookupNames(string email)
    {
        ArgumentNullException.ThrowIfNull(email);

        var at = email.LastIndexOf('@');
        var domain = at < 0 ? null : Normalize(email[(at + 1)..]);
        return domain is null ? [] : SelfAndParents(domain);
    }
}
