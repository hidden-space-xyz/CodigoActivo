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
    /// Projects the current version of a terms document with its content.
    /// </summary>
    public static readonly Expression<Func<TermsDocumentRow, TermsDocumentResponse>> TermsDocument =
        termsDocument => new TermsDocumentResponse
        {
            Id = termsDocument.Id,
            Name = termsDocument.Name,
            Description = termsDocument.Description,
            RequiresSignature = termsDocument.RequiresSignature,
            Kind = termsDocument.Kind,
            Version = termsDocument.Version,
        };
}
