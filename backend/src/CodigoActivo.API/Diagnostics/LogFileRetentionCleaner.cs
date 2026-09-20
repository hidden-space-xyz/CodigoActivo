using System.Text.RegularExpressions;
using CodigoActivo.API.Configuration;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.API.Diagnostics;

/// <summary>
/// Deletes the daily files the API sink leaves in <c>LOG_DIRECTORY</c> once they are older than the
/// retention window. It is the only retention mechanism of that directory, so it runs only when the
/// process logs to files, and it touches nothing but the files the sink writes: the daily
/// <c>api-yyyyMMdd.log</c> and the <c>_NNN</c> files a size rollover adds.
/// <para>
/// A file is removed when its last write is older than 13 days. A daily file holds entries written up
/// to 24 hours before that last write, and the sweep runs every hour, so the oldest entry the
/// directory can hold is 13 days plus 24 hours plus one sweep interval, that is 14 days and one hour,
/// below the 15 days the deployment promises.
/// </para>
/// </summary>
/// <param name="directory">Directory the sink writes its daily files to.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="removeFile">Operation that removes one file, <see cref="File.Delete"/> in the host.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
internal sealed partial class LogFileRetentionCleaner(
    string directory,
    IClock clock,
    Action<string> removeFile,
    ILogger<LogFileRetentionCleaner> logger
) : BackgroundService
{
    private const string SearchPattern = "api-*.log";

    private static readonly TimeSpan Retention = TimeSpan.FromDays(13);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    [GeneratedRegex(@"^api-\d{8}(_\d+)?\.log$", RegexOptions.None, matchTimeoutMilliseconds: 250)]
    private static partial Regex SinkFileName();

    /// <summary>
    /// Registers the cleaner when the configuration selects the file sink. Without a
    /// <c>LOG_DIRECTORY</c> value the process logs to the console and there is nothing to retain.
    /// </summary>
    /// <param name="services">Service collection of the host.</param>
    /// <param name="configuration">Configuration holding the flat <c>LOG_DIRECTORY</c> value.</param>
    internal static void AddLogFileRetention(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var directory = configuration[ApiLogging.DirectoryKey]?.Trim();
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        services.AddHostedService(provider => new LogFileRetentionCleaner(
            directory,
            provider.GetRequiredService<IClock>(),
            File.Delete,
            provider.GetRequiredService<ILogger<LogFileRetentionCleaner>>()
        ));
    }

    /// <summary>
    /// Removes every expired file of the directory. A file that cannot be listed or removed is
    /// logged and never stops the sweep or the following ones.
    /// </summary>
    internal void Sweep()
    {
        try
        {
            foreach (var file in ExpiredFiles())
            {
                Remove(file);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogFileRemovalFailed(ex);
        }
    }

    /// <summary>
    /// Sweeps once at startup and then once per hour until shutdown.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);

        try
        {
            await Task.Yield();

            do
            {
                Sweep();
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    private List<string> ExpiredFiles()
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var writtenBefore = clock.UtcNow.UtcDateTime - Retention;

        return
        [
            .. Directory
                .EnumerateFiles(directory, SearchPattern)
                .Where(path => SinkFileName().IsMatch(Path.GetFileName(path)))
                .Where(path => File.GetLastWriteTimeUtc(path) < writtenBefore),
        ];
    }

    private void Remove(string path)
    {
        try
        {
            removeFile(path);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogFileRemovalFailed(ex);
        }
    }
}
