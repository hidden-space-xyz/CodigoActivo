using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.TermsDocuments.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.TermsDocuments.Queries;

/// <summary>
/// Carries the criteria used to retrieve a terms document by identifier.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
public sealed record GetTermsDocumentByIdQuery(Guid TermsDocumentId)
    : IQuery<Result<TermsDocumentResponse>>;

/// <summary>
/// Executes the query to retrieve a terms document by identifier.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetTermsDocumentByIdQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetTermsDocumentByIdQuery, Result<TermsDocumentResponse>>
{
    /// <summary>
    /// Handles the request to retrieve a terms document by identifier.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the terms document, or an application error when it does not exist.</returns>
    public async Task<Result<TermsDocumentResponse>> HandleAsync(
        GetTermsDocumentByIdQuery query,
        CancellationToken ct = default
    )
    {
        var response = await executor.FirstOrDefaultAsync(
            readStore
                .TermsDocuments.Where(termsDocument => termsDocument.Id == query.TermsDocumentId)
                .Select(TermsDocumentProjections.TermsDocument),
            ct
        );
        return response is null ? Error.NotFound(ErrorCode.TermsDocumentNotFound) : response;
    }
}
