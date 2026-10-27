namespace CodigoActivo.Application.Reports;

/// <summary>
/// Total rows of each table the dashboard summary shows.
/// </summary>
public sealed record DashboardCounts
{
    /// <summary>
    /// Gets the number of events.
    /// </summary>
    public int Events { get; init; }

    /// <summary>
    /// Gets the number of activities.
    /// </summary>
    public int Activities { get; init; }

    /// <summary>
    /// Gets the number of resources.
    /// </summary>
    public int Resources { get; init; }

    /// <summary>
    /// Gets the number of news items.
    /// </summary>
    public int News { get; init; }

    /// <summary>
    /// Gets the number of partners.
    /// </summary>
    public int Partners { get; init; }

    /// <summary>
    /// Gets the number of accounts, dependents included.
    /// </summary>
    public int Users { get; init; }
}
