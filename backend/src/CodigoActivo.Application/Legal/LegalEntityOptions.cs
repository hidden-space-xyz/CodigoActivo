namespace CodigoActivo.Application.Legal;

/// <summary>
/// Legal identity of the association that runs the site, which the privacy policy names as the
/// data controller. Production refuses to start while any value is blank.
/// </summary>
public sealed class LegalEntityOptions
{
    /// <summary>
    /// Longest value accepted for any field.
    /// </summary>
    public const int MaxLength = 300;

    /// <summary>
    /// Gets or sets the registered name of the association.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tax identification number (NIF).
    /// </summary>
    public string TaxId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the registered postal address.
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the register of associations and the entry number in it.
    /// </summary>
    public string Registry { get; set; } = string.Empty;
}
