using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Partners.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Partners.Queries;

/// <summary>
/// Carries the criteria used to retrieve partner by identifier.
/// </summary>
/// <param name="PartnerId">Identifier of the partner.</param>
public sealed record GetPartnerByIdQuery(Guid PartnerId) : IQuery<Result<PartnerResponse>>;

/// <summary>
/// Executes the query to retrieve partner by identifier.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetPartnerByIdQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetPartnerByIdQuery, Result<PartnerResponse>>
{
    /// <summary>
    /// Handles the request to retrieve partner by identifier.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a partner on success, or an application error on failure.</returns>
    public async Task<Result<PartnerResponse>> HandleAsync(
        GetPartnerByIdQuery query,
        CancellationToken ct = default
    )
    {
        var response = await executor.FirstOrDefaultAsync(
            readStore
                .Partners.Where(p => p.Id == query.PartnerId)
                .Select(PartnerProjections.Partner),
            ct
        );
        return response is null ? Error.NotFound(ErrorCode.PartnerNotFound) : response;
    }
}
