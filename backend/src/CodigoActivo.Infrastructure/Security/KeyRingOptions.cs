namespace CodigoActivo.Infrastructure.Security;

/// <summary>
/// Defines where the Data Protection key ring is kept.
/// </summary>
public sealed class KeyRingOptions
{
    /// <summary>
    /// Gets or sets the directory that holds the key ring, the volume mounted in the API container.
    /// </summary>
    public string DirectoryPath { get; set; } = "/home/app/.aspnet/DataProtection-Keys";
}
