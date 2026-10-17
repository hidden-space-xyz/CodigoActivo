using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Logs;

/// <summary>
/// UTC instants a log list is limited to, from the local days its filter names.
/// </summary>
/// <param name="From">First instant included, if any.</param>
/// <param name="Until">First instant excluded, if any.</param>
public sealed record LogPeriod(DateTimeOffset? From, DateTimeOffset? Until)
{
    /// <summary>
    /// Turns a range of local days into UTC instants.
    /// </summary>
    /// <param name="from">First local day, if any.</param>
    /// <param name="to">Last local day, if any.</param>
    /// <param name="zone">Time zone of the days.</param>
    /// <returns>The period.</returns>
    public static LogPeriod Of(DateOnly? from, DateOnly? to, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        return new LogPeriod(
            from is { } first ? LocalDayRange.LowerUtc(first, zone) : null,
            to is { } last ? LocalDayRange.UpperExclusiveUtc(last, zone) : null
        );
    }
}
