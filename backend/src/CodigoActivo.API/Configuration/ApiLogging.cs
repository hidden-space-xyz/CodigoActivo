using CodigoActivo.API.Diagnostics;
using CodigoActivo.Composition.Diagnostics;
using CodigoActivo.Composition.Logs;

namespace CodigoActivo.API.Configuration;

/// <summary>
/// Chooses where the process writes its log. The host stores its events in the
/// <c>diagnostic_log_entries</c> table, and in Development it also writes them to the console.
/// The code that runs before the host exists, or after it failed to start, has a console logger
/// of its own: a start that fails may do so before the database or a valid configuration exists,
/// and the container keeps the console output for that case.
/// <para>
/// The <c>Logging:LogLevel</c> section of the configuration decides every event of the host, for
/// the table exactly as for the console. The startup logger does not read that section: it only
/// emits the fatal startup event and emits it unconditionally.
/// </para>
/// </summary>
internal sealed class ApiLogging : IDisposable
{
    private readonly ILoggerFactory startup = LoggerFactory.Create(AddConsole);

    /// <summary>
    /// Installs the providers of the host: the database, and the console in Development. The
    /// providers have no level filter of their own, so what reaches them is decided by the rules
    /// of <c>Logging:LogLevel</c> the host reads into the builder.
    /// </summary>
    /// <param name="logging">Logging builder of the host.</param>
    /// <param name="environment">Environment of the host.</param>
    internal static void Configure(ILoggingBuilder logging, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(logging);
        ArgumentNullException.ThrowIfNull(environment);

        logging.ClearProviders();
        logging.AddDatabaseLogging();

        if (environment.IsDevelopment())
        {
            AddConsole(logging);
        }
    }

    /// <summary>
    /// Records that the process is ending because starting or running the host failed: always on
    /// the console and, when the host was built, through its providers too.
    /// </summary>
    /// <param name="exception">Failure that ended the process.</param>
    /// <param name="host">Services of the host, when it was built.</param>
    internal void RecordFatal(Exception exception, IServiceProvider? host)
    {
        ArgumentNullException.ThrowIfNull(exception);

        startup.CreateLogger(LogCategories.Lifecycle).HostTerminatedUnexpectedly(exception);
        host?.GetService<ILoggerFactory>()
            ?.CreateLogger(LogCategories.Lifecycle)
            .HostTerminatedUnexpectedly(exception);
    }

    /// <summary>
    /// Flushes and closes the startup console logger.
    /// </summary>
    public void Dispose()
    {
        startup.Dispose();
    }

    private static void AddConsole(ILoggingBuilder logging)
    {
        logging.AddSimpleConsole(options =>
        {
            options.IncludeScopes = false;
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz ";
        });
    }
}
