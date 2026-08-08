using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Partners.Queries;

public sealed record GetPartnerByIdQuery(Guid PartnerId) : IQuery<Result<PartnerResponse>>;

public sealed class GetPartnerByIdQueryHandler(
    IPartnerRepository partners,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<GetPartnerByIdQuery, Result<PartnerResponse>>
{
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
