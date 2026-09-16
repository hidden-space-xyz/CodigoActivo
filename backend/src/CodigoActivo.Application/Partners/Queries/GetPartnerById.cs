using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Partners.Queries;

/// <summary>
/// Carries the criteria used to retrieve partner by identifier.
/// </summary>
/// <param name="PartnerId">Identifier of the partner.</param>
public sealed record GetPartnerByIdQuery(Guid PartnerId) : IQuery<Result<PartnerResponse>>;

/// <summary>
/// Executes the query to retrieve partner by identifier.
/// </summary>
/// <param name="partners">Repository used to persist and retrieve partners.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class GetPartnerByIdQueryHandler(
    IPartnerRepository partners,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<GetPartnerByIdQuery, Result<PartnerResponse>>
{
    /// <summary>
    /// Handles the request to retrieve partner by identifier.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a partner on success, or an application error on failure.</returns>
    public Task<Result<PartnerResponse>> HandleAsync(
        GetPartnerByIdQuery query,
        CancellationToken ct = default
    )
    {
        return cache.GetEntityAsync(
            executor,
            $"partners:id:{query.PartnerId}",
            () => partners.Query().Where(p => p.Id == query.PartnerId).Select(Projections.Partner),
            CacheTags.Partners,
            ErrorCode.PartnerNotFound,
            ct
        );
    }
}
