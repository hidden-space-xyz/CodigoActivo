using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.TermsDocuments.Contracts;

/// <summary>
/// Carries the criteria used to terms document list.
/// </summary>
public sealed class TermsDocumentListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the human-readable name.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Name { get; set; }
}
