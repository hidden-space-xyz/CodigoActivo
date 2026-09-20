namespace CodigoActivo.API.Diagnostics;

/// <summary>
/// Names the logger categories the API configures explicitly.
/// </summary>
internal static class LogCategories
{
    /// <summary>
    /// Category of the process lifecycle events, the only ones allowed to log at
    /// <see cref="LogLevel.Information"/>.
    /// </summary>
    internal const string Lifecycle = "CodigoActivo.Lifecycle";
}
