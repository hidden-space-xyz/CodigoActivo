namespace CodigoActivo.Composition.Diagnostics;

/// <summary>
/// Names the logger categories the application configures explicitly.
/// </summary>
public static class LogCategories
{
    /// <summary>
    /// Category of the event recorded when the process ends because starting or running the host
    /// failed.
    /// </summary>
    public const string Lifecycle = "CodigoActivo.Lifecycle";
}
