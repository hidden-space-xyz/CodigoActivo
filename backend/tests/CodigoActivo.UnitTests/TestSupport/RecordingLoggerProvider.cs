using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.UnitTests.TestSupport;

/// <summary>
/// Logging provider that keeps every entry as "level category: message exception-message".
/// </summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> entries = new();

    public IReadOnlyCollection<string> Entries => [.. entries];

    public ILogger CreateLogger(string categoryName)
    {
        return new Logger(categoryName, entries);
    }

    public void Dispose() { }

    private sealed class Logger(string category, ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            var message = formatter(state, exception);
            entries.Enqueue(
                exception is null
                    ? $"{logLevel} {category}: {message}"
                    : $"{logLevel} {category}: {message} {exception.Message}"
            );
        }
    }
}
