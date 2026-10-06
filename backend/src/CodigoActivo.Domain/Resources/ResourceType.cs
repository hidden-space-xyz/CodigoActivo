namespace CodigoActivo.Domain.Resources;

/// <summary>
/// Whether a resource is written on the site or points to an external page.
/// </summary>
public enum ResourceType
{
    /// <summary>
    /// Resource whose content is a rich-text description.
    /// </summary>
    Internal,

    /// <summary>
    /// Resource that links to an external URL.
    /// </summary>
    External,
}
