using CodigoActivo.Application.Common.Querying;

namespace CodigoActivo.Application.Partners.Contracts;

/// <summary>
/// Carries the criteria used to partner list.
/// </summary>
public sealed class PartnerListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the human-readable name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the website value.
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Gets or sets the tier value.
    /// </summary>
    public int? Tier { get; set; }

    /// <summary>
    /// Gets or sets the from date from value.
    /// </summary>
    public DateOnly? FromDateFrom { get; set; }

    /// <summary>
    /// Gets or sets the from date to value.
    /// </summary>
    public DateOnly? FromDateTo { get; set; }
}
