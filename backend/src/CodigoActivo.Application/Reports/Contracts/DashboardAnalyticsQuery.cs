using System.ComponentModel.DataAnnotations;

namespace CodigoActivo.Application.Reports.Contracts;

/// <summary>
/// Carries the criteria used to dashboard analytics. Both days stay within this century, so the
/// window, its buckets and the previous period it is compared with always remain bounded.
/// </summary>
public sealed class DashboardAnalyticsQuery
{
    private const string MinDay = "2000-01-01";
    private const string MaxDay = "2099-12-31";

    /// <summary>
    /// Gets or sets the from value.
    /// </summary>
    [Range(
        typeof(DateOnly),
        MinDay,
        MaxDay,
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true
    )]
    public DateOnly? From { get; set; }

    /// <summary>
    /// Gets or sets the to value.
    /// </summary>
    [Range(
        typeof(DateOnly),
        MinDay,
        MaxDay,
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true
    )]
    public DateOnly? To { get; set; }
}
