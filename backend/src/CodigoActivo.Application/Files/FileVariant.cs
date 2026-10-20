namespace CodigoActivo.Application.Files;

/// <summary>
/// Size at which a stored image is kept and served.
/// </summary>
public enum FileVariant
{
    /// <summary>
    /// Image at the size pages and rich text show it.
    /// </summary>
    Full,

    /// <summary>
    /// Smaller copy for lists, cards and logos.
    /// </summary>
    Thumbnail,
}
