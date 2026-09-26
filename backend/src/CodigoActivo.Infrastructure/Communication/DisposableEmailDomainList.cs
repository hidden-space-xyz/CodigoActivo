using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Identifies why a downloaded disposable email domain list was not stored.
/// </summary>
public enum DisposableEmailDomainListRejection
{
    /// <summary>
    /// Selects the option for a body larger than <see cref="DisposableEmailDomainList.MaxBytes"/>.
    /// </summary>
    TooLarge,

    /// <summary>
    /// Selects the option for a body that is not UTF-8 text.
    /// </summary>
    NotText,

    /// <summary>
    /// Selects the option for a line that is neither blank, a comment nor a domain name.
    /// </summary>
    MalformedEntry,

    /// <summary>
    /// Selects the option for a list with fewer domains than
    /// <see cref="DisposableEmailDomainList.MinDomains"/>.
    /// </summary>
    TooFewDomains,

    /// <summary>
    /// Selects the option for a list that would refuse one of the
    /// <see cref="DisposableEmailDomainList.ProtectedDomains"/>.
    /// </summary>
    ProtectedDomainListed,
}

/// <summary>
/// Contains the outcome of reading a downloaded disposable email domain list.
/// </summary>
/// <param name="Domains">Validated normalized domains, empty when the list was rejected.</param>
/// <param name="Rejection">Reason the list was rejected, or <see langword="null"/> when it is valid.</param>
public sealed record DisposableEmailDomainListResult(
    IReadOnlySet<string> Domains,
    DisposableEmailDomainListRejection? Rejection
)
{
    /// <summary>
    /// Creates the outcome of a list that passed every check.
    /// </summary>
    /// <param name="domains">Validated normalized domains.</param>
    /// <returns>The valid outcome.</returns>
    public static DisposableEmailDomainListResult Valid(IReadOnlySet<string> domains)
    {
        return new(domains, null);
    }

    /// <summary>
    /// Creates the outcome of a rejected list.
    /// </summary>
    /// <param name="rejection">Reason the list was rejected.</param>
    /// <returns>The rejected outcome.</returns>
    public static DisposableEmailDomainListResult Rejected(
        DisposableEmailDomainListRejection rejection
    )
    {
        return new(FrozenSet<string>.Empty, rejection);
    }
}

/// <summary>
/// Validates a downloaded disposable email domain list before it replaces the stored one. The list
/// is UTF-8 text with one domain per line; blank lines and lines starting with <c>#</c> are skipped.
/// Any other line that is not a domain name rejects the whole list, and so does a list that is too
/// large, too short or that would refuse a widely used mailbox provider, so a source that
/// disappears, changes its format or is tampered with never replaces the last valid list.
/// </summary>
public static partial class DisposableEmailDomainList
{
    /// <summary>
    /// Largest body accepted: 2 MiB, about sixteen times the size of the published list.
    /// </summary>
    public const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>
    /// Fewest domains a genuine list holds; the published list has several thousand.
    /// </summary>
    public const int MinDomains = 1000;

    /// <summary>
    /// Domains of widely used mailbox providers that no genuine list refuses. A list that would
    /// refuse any of them, directly or through a parent domain, is treated as corrupted or
    /// tampered with.
    /// </summary>
    public static readonly FrozenSet<string> ProtectedDomains = new[]
    {
        "gmail.com",
        "hotmail.com",
        "hotmail.es",
        "icloud.com",
        "outlook.com",
        "outlook.es",
        "proton.me",
        "protonmail.com",
        "yahoo.com",
        "yahoo.es",
        "codigoactivo.es"
    }.ToFrozenSet(StringComparer.Ordinal);

    private const int MaxDomainLength = 253;

    private const char ByteOrderMark = (char)0xFEFF;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    [GeneratedRegex(
        @"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\z",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 250
    )]
    private static partial Regex DomainLabel();

    /// <summary>
    /// Reads and validates a downloaded list.
    /// </summary>
    /// <param name="content">Raw body of the download.</param>
    /// <returns>The normalized domains of a valid list, or the reason the list was rejected.</returns>
    public static DisposableEmailDomainListResult Parse(ReadOnlySpan<byte> content)
    {
        if (content.Length > MaxBytes)
        {
            return DisposableEmailDomainListResult.Rejected(
                DisposableEmailDomainListRejection.TooLarge
            );
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(content);
        }
        catch (DecoderFallbackException)
        {
            return DisposableEmailDomainListResult.Rejected(
                DisposableEmailDomainListRejection.NotText
            );
        }

        var domains = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in text.AsSpan().TrimStart(ByteOrderMark).EnumerateLines())
        {
            var entry = line.Trim();
            if (entry.IsEmpty || entry[0] is '#')
            {
                continue;
            }

            var domain = EmailDomains.Normalize(entry.ToString());
            if (domain is null || !IsDomainName(domain))
            {
                return DisposableEmailDomainListResult.Rejected(
                    DisposableEmailDomainListRejection.MalformedEntry
                );
            }

            domains.Add(domain);
        }

        if (domains.Count < MinDomains)
        {
            return DisposableEmailDomainListResult.Rejected(
                DisposableEmailDomainListRejection.TooFewDomains
            );
        }

        if (
            ProtectedDomains.Any(provider =>
                EmailDomains.SelfAndParents(provider).Any(domains.Contains)
            )
        )
        {
            return DisposableEmailDomainListResult.Rejected(
                DisposableEmailDomainListRejection.ProtectedDomainListed
            );
        }

        return DisposableEmailDomainListResult.Valid(domains.ToFrozenSet(StringComparer.Ordinal));
    }

    private static bool IsDomainName(string domain)
    {
        var labels = domain.Split('.');
        return domain.Length <= MaxDomainLength
            && labels.Length >= 2
            && labels.All(label => DomainLabel().IsMatch(label));
    }
}
