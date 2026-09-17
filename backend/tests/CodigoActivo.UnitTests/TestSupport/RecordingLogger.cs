using Microsoft.Extensions.Logging;

namespace CodigoActivo.UnitTests.TestSupport;

/// <summary>
/// Logger that keeps every formatted entry, together with its exception, for assertions.
/// </summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<string> entries = [];

    /// <summary>Formatted messages followed by the text of their exception, if any.</summary>
    public IReadOnlyList<string> Entries
    {
        get
        {
            lock (entries)
            {
                return [.. entries];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        var entry = exception is null
            ? formatter(state, exception)
            : $"{formatter(state, exception)} {exception}";
        lock (entries)
        {
            entries.Add(entry);
        }
    }
}
