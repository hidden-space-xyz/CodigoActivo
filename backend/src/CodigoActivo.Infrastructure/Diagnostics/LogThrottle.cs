namespace CodigoActivo.Infrastructure.Diagnostics;

/// <summary>
/// Lets an operational event that repeats while a failure lasts through at most once per interval:
/// the first occurrence is logged and the repeats within the interval are dropped. Concurrent
/// callers may share one instance.
/// </summary>
/// <param name="interval">Minimum time between two occurrences let through.</param>
public sealed class LogThrottle(TimeSpan interval)
{
    private readonly Lock gate = new();
    private DateTimeOffset? lastAt;

    /// <summary>
    /// Tells whether an occurrence at <paramref name="now"/> is let through, which starts a new
    /// interval.
    /// </summary>
    /// <param name="now">Time of the occurrence.</param>
    /// <returns><see langword="true"/> when the occurrence is to be logged.</returns>
    public bool TryEnter(DateTimeOffset now)
    {
        lock (gate)
        {
            if (lastAt is { } last && now - last < interval)
            {
                return false;
            }

            lastAt = now;
            return true;
        }
    }
}
