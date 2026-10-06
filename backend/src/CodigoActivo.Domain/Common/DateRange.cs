namespace CodigoActivo.Domain.Common;

/// <summary>
/// Calendar days from a first to a last day, both included.
/// </summary>
public sealed record DateRange
{
    private DateRange(DateOnly start, DateOnly end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Gets the first day.
    /// </summary>
    public DateOnly Start { get; }

    /// <summary>
    /// Gets the last day.
    /// </summary>
    public DateOnly End { get; }

    /// <summary>
    /// Creates a range when its last day is not before its first day.
    /// </summary>
    /// <param name="start">First day.</param>
    /// <param name="end">Last day.</param>
    /// <returns>The range, or <see langword="null"/> when it ends before it starts.</returns>
    public static DateRange? TryCreate(DateOnly start, DateOnly end)
    {
        return end < start ? null : new DateRange(start, end);
    }

    /// <summary>
    /// Restores a range that was checked before it was stored.
    /// </summary>
    /// <param name="start">Stored first day.</param>
    /// <param name="end">Stored last day.</param>
    /// <returns>The range.</returns>
    public static DateRange FromStored(DateOnly start, DateOnly end)
    {
        return new DateRange(start, end);
    }

    /// <summary>
    /// Tells whether a day falls within the range.
    /// </summary>
    /// <param name="day">Day to check.</param>
    /// <returns><see langword="true"/> when the day is between the first and the last day.</returns>
    public bool Contains(DateOnly day)
    {
        return day >= Start && day <= End;
    }

    /// <summary>
    /// Tells whether the whole range lies before a day.
    /// </summary>
    /// <param name="day">Day to compare with.</param>
    /// <returns><see langword="true"/> when the last day is before <paramref name="day"/>.</returns>
    public bool EndsBefore(DateOnly day)
    {
        return End < day;
    }
}
