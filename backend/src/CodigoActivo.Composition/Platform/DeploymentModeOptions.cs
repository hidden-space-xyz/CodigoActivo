namespace CodigoActivo.Composition.Platform;

/// <summary>
/// Deployment mode chosen with <c>DEMO_MODE</c>, locked on the first start.
/// </summary>
internal sealed class DeploymentModeOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the deployment runs in demo mode.
    /// </summary>
    public bool Demo { get; set; }
}
