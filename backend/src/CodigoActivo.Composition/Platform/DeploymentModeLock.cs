using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CodigoActivo.Composition.Platform;

/// <summary>
/// Freezes the deployment mode validated at start. The choice is always persisted, in the
/// <c>api-state</c> volume when the process runs in a container, except for a Development process
/// outside a container: a developer running the API directly keeps no state on their own
/// filesystem.
/// </summary>
internal sealed class DeploymentModeLock
{
    private const string PersistentFilePath = "/app/state/deployment-mode";
    private const string ContainerKey = "DOTNET_RUNNING_IN_CONTAINER";
    private readonly string filePath;

    /// <summary>
    /// Initializes a deployment mode lock persisted in the <c>api-state</c> volume.
    /// </summary>
    public DeploymentModeLock()
        : this(PersistentFilePath) { }

    internal DeploymentModeLock(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = Path.GetFullPath(filePath);
    }

    /// <summary>
    /// Refuses a start with a mode other than the stored one without writing anything, so it can
    /// run before any other step changes state. A first start has nothing to compare and passes.
    /// </summary>
    /// <param name="demoMode">Whether the configured mode is demo mode.</param>
    /// <param name="configuration">Configuration holding the container marker.</param>
    /// <param name="environment">Host environment that decides whether the choice is persisted.</param>
    /// <exception cref="InvalidOperationException">
    /// The stored mode is unreadable or differs from the configured one.
    /// </exception>
    public void Check(bool demoMode, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (IsPersisted(configuration, environment) && File.Exists(filePath))
        {
            Verify(demoMode);
        }
    }

    /// <summary>
    /// Stores the configured mode on the first start and refuses any later start with the other
    /// mode, unless it is a Development process outside a container.
    /// </summary>
    /// <param name="demoMode">Whether the configured mode is demo mode.</param>
    /// <param name="configuration">Configuration holding the container marker.</param>
    /// <param name="environment">Host environment that decides whether the choice is persisted.</param>
    /// <exception cref="InvalidOperationException">
    /// The stored mode is unreadable or differs from the configured one.
    /// </exception>
    public void Lock(bool demoMode, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (!IsPersisted(configuration, environment))
        {
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("The deployment mode path is invalid.");
        }

        Directory.CreateDirectory(directory);
        if (!File.Exists(filePath))
        {
            CreateLockFile(ModeName(demoMode));
        }

        Verify(demoMode);
    }

    private void Verify(bool demoMode)
    {
        var configuredValue = ModeName(demoMode);
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
            return;
        }
    }

    private static bool IsPersisted(IConfiguration configuration, IHostEnvironment environment)
    {
        return !environment.IsDevelopment() || RunsInContainer(configuration);
    }

    private static bool RunsInContainer(IConfiguration configuration)
    {
        var value = configuration[ContainerKey]?.Trim();
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.Ordinal);
    }

    private static string ModeName(bool demoMode)
    {
        return demoMode ? "demo" : "normal";
    }
}
