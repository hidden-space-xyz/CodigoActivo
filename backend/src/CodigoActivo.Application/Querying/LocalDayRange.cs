namespace CodigoActivo.Application.Querying;

public static class LocalDayRange
{
    public static DateTimeOffset LowerUtc(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return zone.IsInvalidTime(local)
            ? new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime()
            : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    public static DateTimeOffset UpperExclusiveUtc(DateOnly day, TimeZoneInfo zone)
    {
        return LowerUtc(day.AddDays(1), zone);
    }
}
