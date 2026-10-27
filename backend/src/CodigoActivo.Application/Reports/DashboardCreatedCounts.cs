namespace CodigoActivo.Application.Reports;

/// <summary>
/// How many rows of each dashboard source were created in each interval of the thresholds. With
/// the thresholds numbered from 0, interval 0 holds what was created before threshold 0 and
/// interval <c>n</c> what was created at or after threshold <c>n - 1</c> and before threshold
/// <c>n</c>; the last interval, after the last threshold, is open-ended. Empty intervals are left
/// out.
/// </summary>
/// <param name="Users">Accounts per membership type catalog identifier.</param>
/// <param name="Inscriptions">Signups per status catalog identifier.</param>
/// <param name="Events">Events created per interval.</param>
/// <param name="Activities">Activities created per interval.</param>
/// <param name="Resources">Resources created per interval.</param>
/// <param name="News">News items created per interval.</param>
/// <param name="Partners">Partners created per interval.</param>
public sealed record DashboardCreatedCounts(
    IReadOnlyList<KeyedIntervalCount> Users,
    IReadOnlyList<KeyedIntervalCount> Inscriptions,
    IReadOnlyList<IntervalCount> Events,
    IReadOnlyList<IntervalCount> Activities,
    IReadOnlyList<IntervalCount> Resources,
    IReadOnlyList<IntervalCount> News,
    IReadOnlyList<IntervalCount> Partners
);

/// <summary>
/// Rows created in one interval.
/// </summary>
/// <param name="Interval">Index of the interval.</param>
/// <param name="Count">Rows created in it.</param>
public sealed record IntervalCount(int Interval, int Count);

/// <summary>
/// Rows of one catalog member created in one interval.
/// </summary>
/// <param name="Key">Catalog identifier the rows carry.</param>
/// <param name="Interval">Index of the interval.</param>
/// <param name="Count">Rows created in it.</param>
public sealed record KeyedIntervalCount(Guid Key, int Interval, int Count);
