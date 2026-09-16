namespace CodigoActivo.Domain.Entities.Abstractions;

/// <summary>
/// Defines the operations required to work with featurable.
/// </summary>
public interface IFeaturable
{
    /// <summary>
    /// Gets or sets whether the item is highlighted as featured.
    /// </summary>
    public bool Featured { get; set; }
}
