using System.Text;

namespace CodigoActivo.API.Security;

/// <summary>
/// Freezes deployment mode settings after startup validation. The choice is always persisted, in
/// the <c>api-state</c> volume when the process runs in a container, except for a Development
/// process outside a container: a developer running the API directly validates the configured value
/// and keeps no state on their own filesystem.
/// </summary>
public sealed partial class DeploymentModeLock
{
    private const string PersistentFilePath = "/app/state/deployment-mode";
    private const string ContainerKey = "DOTNET_RUNNING_IN_CONTAINER";
    private readonly string filePath;
    private readonly ILogger<DeploymentModeLock> logger;

    /// <summary>
    /// Initializes a deployment mode lock with its required dependencies.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    public DeploymentModeLock(ILogger<DeploymentModeLock> logger)
        : this(PersistentFilePath, logger) { }

    internal DeploymentModeLock(string filePath, ILogger<DeploymentModeLock> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(logger);
        this.filePath = Path.GetFullPath(filePath);
        this.logger = logger;
    }

    /// <summary>
    /// Validates deployment settings and prevents later mutation.
    /// </summary>
    /// <param name="configuration">Application configuration to validate or consume.</param>
    /// <param name="environment">Host environment that decides whether the choice is persisted.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool Lock(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var configuredMode = ReadConfiguredMode(configuration["DEMO_MODE"]);
        var configuredValue = configuredMode ? "demo" : "normal";
        if (environment.IsDevelopment() && !RunsInContainer(configuration))
        {
            LogLockSkipped(configuredValue);
            return configuredMode;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("The deployment mode path is invalid.");
        }

        Directory.CreateDirectory(directory);
        if (!File.Exists(filePath))
        {
            CreateLockFile(configuredValue);
        }

        var persistedValue = File.ReadAllText(filePath, Encoding.UTF8).Trim();
        if (persistedValue is not ("demo" or "normal"))
        {
            throw new InvalidOperationException(
                "The persistent deployment mode file is invalid. Recreate all application containers and volumes."
            );
        }

        if (!string.Equals(persistedValue, configuredValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"This deployment was initialized in {persistedValue} mode and cannot start in {configuredValue} mode. "
                    + "Recreate all application containers and volumes to choose a different mode."
            );
        }

        return configuredMode;
    }

    private void CreateLockFile(string configuredValue)
    {
        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.WriteThrough
            );
            var contents = Encoding.UTF8.GetBytes(configuredValue + "\n");
            stream.Write(contents);
            stream.Flush(flushToDisk: true);

            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch (IOException) when (File.Exists(filePath))
        {
            // A concurrent process created the immutable lock first. The caller validates it next.
            return;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Deployment mode lock skipped: this is a Development process outside a container, "
            + "so the selected {ConfiguredMode} mode is not persisted"
    )]
    private partial void LogLockSkipped(string configuredMode);

    private static bool RunsInContainer(IConfiguration configuration)
    {
        var value = configuration[ContainerKey]?.Trim();
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.Ordinal);
    }

    private static bool ReadConfiguredMode(string? value)
    {
        if (bool.TryParse(value, out var mode))
        {
            return mode;
        }

        throw new InvalidOperationException("DEMO_MODE must be either true or false.");
    }
}
