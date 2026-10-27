using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.TermsDocuments.Contracts;

/// <summary>
/// Carries the criteria used to list terms documents.
/// </summary>
public sealed class TermsDocumentListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the text the name must contain, ignoring case and accents; every document when
    /// empty.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Name { get; set; }
}
