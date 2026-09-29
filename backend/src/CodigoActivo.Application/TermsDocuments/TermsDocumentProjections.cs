using System.Linq.Expressions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.TermsDocuments.Contracts;

namespace CodigoActivo.Application.TermsDocuments;

/// <summary>
/// Database projections from the read models to the response shapes of this feature.
/// </summary>
public static class TermsDocumentProjections
{
    /// <summary>
    /// Stores the shared terms document value.
    /// </summary>
    public static readonly Expression<Func<TermsDocumentRow, TermsDocumentResponse>> TermsDocument =
        termsDocument => new TermsDocumentResponse
        {
            Id = termsDocument.Id,
            Name = termsDocument.Name,
            Description = termsDocument.Description,
        };
}
