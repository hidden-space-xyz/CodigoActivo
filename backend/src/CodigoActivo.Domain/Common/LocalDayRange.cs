namespace CodigoActivo.Domain.Common;

/// <summary>
/// Converts a local calendar day into the UTC instants it covers, so a day counted in a time zone
/// can be compared with stored instants.
/// </summary>
public static class LocalDayRange
{
    /// <summary>
    /// Calculates the first instant of a local day. When the local midnight does not exist because the
    /// clocks skip it, the day starts at the instant that midnight would have had; when it happens
    /// twice because the clocks go back, the day starts at the first one.
    /// </summary>
    /// <param name="day">Local day.</param>
    /// <param name="zone">Time zone the day is counted in.</param>
    /// <returns>The first instant of the day, in UTC.</returns>
    public static DateTimeOffset LowerUtc(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        if (zone.IsInvalidTime(local))
        {
            return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
        }

        return zone.IsAmbiguousTime(local)
            ? new DateTimeOffset(local, zone.GetAmbiguousTimeOffsets(local).Max()).ToUniversalTime()
            : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    /// <summary>
    /// Calculates the first instant after a local day. The last representable day has no following
    /// day, so its range extends to <see cref="DateTimeOffset.MaxValue"/>.
    /// </summary>
    /// <param name="day">Local day.</param>
    /// <param name="zone">Time zone the day is counted in.</param>
    /// <returns>The first instant after the day, in UTC.</returns>
    public static DateTimeOffset UpperExclusiveUtc(DateOnly day, TimeZoneInfo zone)
    {
        return day == DateOnly.MaxValue ? DateTimeOffset.MaxValue : LowerUtc(day.AddDays(1), zone);
    }
}
