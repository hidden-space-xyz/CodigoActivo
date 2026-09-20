using System.Text;
using CodigoActivo.API.Diagnostics;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Formatting;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CodigoActivo.API.Configuration;

/// <summary>
/// Chooses where the process writes its log. With a <c>LOG_DIRECTORY</c> value every event goes to a
/// daily file in that directory and nothing to the console; without one the console is used, which is
/// the local development case. The same instance also serves the startup code that runs before the
/// host exists, so a failed validation or a fatal exception reaches the file too. Old files are never
/// removed here: retention belongs to <see cref="Diagnostics.LogFileRetentionCleaner"/>.
/// <para>
/// The <c>Logging:LogLevel</c> section of the configuration decides every event of the host, for the
/// file exactly as for the console: the sink accepts every level it is handed and adds no policy of
/// its own. The logger that serves the code running before the host does not read that section: it
/// only emits the fatal startup event and emits it unconditionally, because a startup that fails may
/// do so before a valid configuration exists.
/// </para>
/// </summary>
internal sealed class ApiLogging : IDisposable
{
    internal const string DirectoryKey = "LOG_DIRECTORY";

    private const string FileNamePattern = "api-.log";
    private const long FileSizeLimitBytes = 64L * 1024 * 1024;

    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);

    private readonly Serilog.Core.Logger? fileLogger;
    private readonly ILoggerFactory factory;

    private ApiLogging(Serilog.Core.Logger? fileLogger)
    {
        this.fileLogger = fileLogger;
        factory = LoggerFactory.Create(Configure);
    }

    /// <summary>
    /// Opens the configured sink and returns the logging the process uses from now on. In file mode the
    /// daily file is opened here, so a destination that cannot be written stops the process instead of
    /// leaving it running without any log.
    /// </summary>
    /// <param name="configuration">Configuration holding the flat <c>LOG_DIRECTORY</c> value.</param>
    /// <returns>The logging shared by the startup code and the host.</returns>
    /// <exception cref="InvalidOperationException">
    /// The daily file of the configured directory cannot be opened for writing.
    /// </exception>
    internal static ApiLogging Start(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var directory = configuration[DirectoryKey]?.Trim();
        return new ApiLogging(string.IsNullOrEmpty(directory) ? null : CreateFileLogger(directory));
    }

    /// <summary>
    /// Creates a logger usable before the host is built. It applies no level rule: the fatal startup
    /// event reaches the sink even when <c>Logging:LogLevel</c> silences everything or is missing.
    /// </summary>
    /// <param name="category">Category the entries are written under.</param>
    /// <returns>A logger writing to the configured sink.</returns>
    internal ILogger CreateLogger(string category)
    {
        return factory.CreateLogger(category);
    }

    /// <summary>
    /// Installs the configured sink as the only logging provider of the host. The provider is added
    /// without a level filter of its own, so what reaches the sink is decided by the rules of
    /// <c>Logging:LogLevel</c> the host reads into the builder.
    /// </summary>
    /// <param name="logging">Logging builder of the host.</param>
    internal void Configure(ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);

        logging.ClearProviders();

        if (fileLogger is null)
        {
            logging.AddSimpleConsole(options =>
            {
                options.IncludeScopes = false;
                options.SingleLine = true;
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz ";
            });
            return;
        }

        logging.AddProvider(new SerilogLoggerProvider(fileLogger, dispose: false));
    }

    internal static Serilog.Core.Logger CreateFileLogger(string directory)
    {
        Directory.CreateDirectory(directory);
        EnsureDailyFileIsWritable(directory);

        return WriteToDailyFile(
                new LoggerConfiguration().MinimumLevel.Verbose().WriteTo,
                directory,
                new SingleLineLogFormatter(),
                FlushInterval
            )
            .CreateLogger();
    }

    private static LoggerConfiguration WriteToDailyFile(
        LoggerSinkConfiguration sink,
        string directory,
        ITextFormatter formatter,
        TimeSpan? flushToDiskInterval
    )
    {
        return sink.File(
            formatter,
            Path.Combine(directory, FileNamePattern),
            fileSizeLimitBytes: FileSizeLimitBytes,
            rollingInterval: RollingInterval.Day,
            rollOnFileSizeLimit: true,
            retainedFileCountLimit: null,
            retainedFileTimeLimit: null,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            flushToDiskInterval: flushToDiskInterval
        );
    }

    private static void EnsureDailyFileIsWritable(string directory)
    {
        var failures = new SinkFailureRecorder();
        Exception? thrown = null;

        try
        {
            using var probe = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Fallible(
                    sink =>
                        WriteToDailyFile(
                            sink,
                            directory,
                            SilentFormatter.Instance,
                            flushToDiskInterval: null
                        ),
                    failures
                )
                .CreateLogger();

            probe.Write(LogEventLevel.Verbose, "Daily log file probe");
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        var failure = failures.Failure ?? thrown?.Message;
        if (failure is null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"The daily log file of '{directory}' cannot be opened for writing: {failure}",
            thrown
        );
    }

    private sealed class SilentFormatter : ITextFormatter
    {
        internal static readonly SilentFormatter Instance = new();

        public void Format(LogEvent logEvent, TextWriter output) { }
    }

    private sealed class SinkFailureRecorder : ILoggingFailureListener
    {
        internal string? Failure { get; private set; }

        public void OnLoggingFailed(
            object sender,
            LoggingFailureKind kind,
            string message,
            IReadOnlyCollection<LogEvent>? events,
            Exception? exception
        )
        {
            if (Failure is not null || (kind == LoggingFailureKind.Temporary && exception is null))
            {
                return;
            }

            Failure = exception is null ? message : $"{message}: {exception.Message}";
        }
    }

    /// <summary>
    /// Flushes and closes the sink.
    /// </summary>
    public void Dispose()
    {
        factory.Dispose();
        fileLogger?.Dispose();
    }
}
