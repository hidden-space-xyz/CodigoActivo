using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Users.Queries;

public sealed record ListUserTypesQuery : IQuery<IReadOnlyList<UserTypeResponse>>;

public sealed class ListUserTypesQueryHandler(
    IUserTypeRepository userTypes,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<ListUserTypesQuery, IReadOnlyList<UserTypeResponse>>
{
    public Task<IReadOnlyList<UserTypeResponse>> HandleAsync(
        ListUserTypesQuery query,
        CancellationToken ct = default
    )
    {
        return cache.GetCatalogAsync(
            executor,
            "users:types",
            () => userTypes.Query().OrderBy(type => type.Name).Select(Projections.UserType),
            ct
        );
    }
}
