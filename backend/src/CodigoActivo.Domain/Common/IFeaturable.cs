namespace CodigoActivo.Domain.Common;

/// <summary>
/// Content that can be highlighted as the featured one of its kind.
/// </summary>
public interface IFeaturable
{
    /// <summary>
    /// Gets a value indicating whether the item is highlighted as featured.
    /// </summary>
    public bool Featured { get; }

    /// <summary>
    /// Highlights the item. It does not count as an edit of its content.
    /// </summary>
    public void Feature();

    /// <summary>
    /// Stops highlighting the item. It does not count as an edit of its content.
    /// </summary>
    public void Unfeature();
}
