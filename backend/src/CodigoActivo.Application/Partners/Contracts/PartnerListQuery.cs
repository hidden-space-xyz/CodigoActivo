using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.Partners.Contracts;

/// <summary>
/// Carries the criteria used to list partners; every criterion left empty matches every partner.
/// Text criteria match a part of the text, ignoring case and accents.
/// </summary>
public sealed class PartnerListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the text the name must contain.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the text the website must contain.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Website { get; set; }

    /// <summary>
    /// Gets or sets the sponsorship tier the partner must have.
    /// </summary>
    public int? Tier { get; set; }

    /// <summary>
    /// Gets or sets the earliest day the collaboration may have started.
    /// </summary>
    public DateOnly? FromDateFrom { get; set; }

    /// <summary>
    /// Gets or sets the latest day the collaboration may have started.
    /// </summary>
    public DateOnly? FromDateTo { get; set; }
}
