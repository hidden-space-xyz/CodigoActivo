using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.IntegrationTests.Infrastructure;

/// <summary>
/// Keeps every entry the host lets through its configured level policy, so a test can prove that a
/// scenario leaves nothing in the production log.
/// </summary>
public sealed class LogRecorder : ILoggerProvider
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
            entries.Enqueue($"{logLevel} {category}: {formatter(state, exception)}");
        }
    }
}
