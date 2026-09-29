using CodigoActivo.Application.Common.Querying;

namespace CodigoActivo.Application.TermsDocuments.Contracts;

/// <summary>
/// Carries the criteria used to terms document list.
/// </summary>
public sealed class TermsDocumentListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the human-readable name.
    /// </summary>
    public string? Name { get; set; }
}
