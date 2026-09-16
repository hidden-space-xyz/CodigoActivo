using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Partners.Queries;

/// <summary>
/// Carries the criteria used to list partners.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListPartnersQuery(PartnerListQuery Filters)
    : IQuery<PagedResult<PartnerResponse>>;

/// <summary>
/// Executes the query to list partners.
/// </summary>
/// <param name="partners">Repository used to persist and retrieve partners.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class ListPartnersQueryHandler(
    IPartnerRepository partners,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<ListPartnersQuery, PagedResult<PartnerResponse>>
{
    private static readonly SortMap<PartnerResponse> Sort = new SortMap<PartnerResponse>()
        .Add("name", p => p.Name)
        .Add("tier", p => p.Tier)
        .Add("website", p => p.Website)
        .Add("fromDate", p => p.FromDate)
        .Add("createdAt", p => p.CreatedAt)
        .Default("tier", "-fromDate")
        .Tie(p => p.Id);

    /// <summary>
    /// Handles the request to list partners.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a paged partner.</returns>
    public async Task<PagedResult<PartnerResponse>> HandleAsync(
        ListPartnersQuery query,
        CancellationToken ct = default
    )
    {
        return await cache.GetOrCreateAsync(
            CacheKeys.For("partners:list", query.Filters),
            token => new ValueTask<PagedResult<PartnerResponse>>(FetchAsync(query.Filters, token)),
            CachePolicies.PublicContent,
            [CacheTags.Partners],
            ct
        );
    }

    private Task<PagedResult<PartnerResponse>> FetchAsync(
        PartnerListQuery query,
        CancellationToken ct
    )
    {
        var source = partners.Query().Select(Projections.Partner);

        if (query.Tier is { } tier)
        {
            source = source.Where(p => p.Tier == tier);
        }

        if (query.FromDateFrom is { } fromDateFrom)
        {
            source = source.Where(p => p.FromDate >= fromDateFrom);
        }

        if (query.FromDateTo is { } fromDateTo)
        {
            source = source.Where(p => p.FromDate <= fromDateTo);
        }

        source = source.WhereContains(p => p.Name, query.Name);
        source = source.WhereContains(p => p.Website, query.Website);

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }
}
