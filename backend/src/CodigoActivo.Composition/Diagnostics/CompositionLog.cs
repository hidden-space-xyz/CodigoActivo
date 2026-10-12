using Microsoft.Extensions.Logging;

namespace CodigoActivo.Composition.Diagnostics;

/// <summary>
/// Declares the events the composition root records while it prepares the process to start.
/// </summary>
internal static partial class CompositionLog
{
    /// <summary>
    /// Records that the deployment mode was validated but not persisted, which only happens in a
    /// Development process running outside a container.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="configuredMode">Deployment mode the configuration selected.</param>
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Deployment mode lock skipped: this is a Development process outside a container, "
            + "so the selected {ConfiguredMode} mode is not persisted"
    )]
    internal static partial void DeploymentModeLockSkipped(
        this ILogger logger,
        string configuredMode
    );
}
