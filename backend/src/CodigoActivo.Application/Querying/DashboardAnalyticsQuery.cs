namespace CodigoActivo.Application.Querying;

/// <summary>
/// Carries the criteria used to dashboard analytics.
/// </summary>
public sealed class DashboardAnalyticsQuery
{
    /// <summary>
    /// Gets or sets the from value.
    /// </summary>
    public DateOnly? From { get; set; }

    /// <summary>
    /// Gets or sets the to value.
    /// </summary>
    public DateOnly? To { get; set; }
}
