namespace CodigoActivo.Application.Querying;

/// <summary>
/// Converts a local calendar day into its inclusive UTC time range.
/// </summary>
public static class LocalDayRange
{
    /// <summary>
    /// Calculates the lower utc boundary.
    /// </summary>
    /// <param name="day">The day value.</param>
    /// <param name="zone">The zone value.</param>
    /// <returns>The resulting date time offset value.</returns>
    public static DateTimeOffset LowerUtc(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return zone.IsInvalidTime(local)
            ? new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime()
            : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    /// <summary>
    /// Calculates the upper exclusive utc boundary.
    /// </summary>
    /// <param name="day">The day value.</param>
    /// <param name="zone">The zone value.</param>
    /// <returns>The resulting date time offset value.</returns>
    public static DateTimeOffset UpperExclusiveUtc(DateOnly day, TimeZoneInfo zone)
    {
        return LowerUtc(day.AddDays(1), zone);
    }
}
