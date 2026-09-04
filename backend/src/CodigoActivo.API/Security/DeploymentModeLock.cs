using System.Text;

namespace CodigoActivo.API.Security;

public sealed class DeploymentModeLock
{
    private const string PersistentFilePath = "/app/state/deployment-mode";
    private readonly string filePath;

    public DeploymentModeLock()
        : this(PersistentFilePath) { }

    internal DeploymentModeLock(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = Path.GetFullPath(filePath);
    }

    public bool Lock(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredMode = ReadConfiguredMode(configuration["DEMO_MODE"]);
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("The deployment mode path is invalid.");
        }

        Directory.CreateDirectory(directory);
        var configuredValue = configuredMode ? "demo" : "normal";
        if (!File.Exists(filePath))
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
                    File.SetUnixFileMode(
                        filePath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite
                    );
                }
            }
            catch (IOException) when (File.Exists(filePath))
            {
            }
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

    private static bool ReadConfiguredMode(string? value)
    {
        if (bool.TryParse(value, out var mode))
        {
            return mode;
        }

        throw new InvalidOperationException("DEMO_MODE must be either true or false.");
    }
}
