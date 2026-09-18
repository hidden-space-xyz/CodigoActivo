using Microsoft.Extensions.Logging;

namespace CodigoActivo.UnitTests.TestSupport;

/// <summary>
/// Logger that keeps every formatted entry, together with its exception, for assertions.
/// </summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<string> entries = [];
    private readonly List<(LogLevel Level, string Message)> levelEntries = [];

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

    /// <summary>Every recorded entry together with the level it was logged at.</summary>
    public IReadOnlyList<(LogLevel Level, string Message)> LevelEntries
    {
        get
        {
            lock (entries)
            {
                return [.. levelEntries];
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
            levelEntries.Add((logLevel, entry));
        }
    }
}
