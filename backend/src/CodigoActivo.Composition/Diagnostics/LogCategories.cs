namespace CodigoActivo.Composition.Diagnostics;

/// <summary>
/// Names the logger categories the application configures explicitly.
/// </summary>
public static class LogCategories
{
    /// <summary>
    /// Category of the process lifecycle events, the only ones allowed to log at the information
    /// level.
    /// </summary>
    public const string Lifecycle = "CodigoActivo.Lifecycle";
}
