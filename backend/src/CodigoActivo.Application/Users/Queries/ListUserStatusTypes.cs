using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Users.Queries;

public sealed record ListUserStatusTypesQuery : IQuery<IReadOnlyList<UserStatusTypeResponse>>;

public sealed class ListUserStatusTypesQueryHandler(
    IUserStatusTypeRepository userStatusTypes,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<ListUserStatusTypesQuery, IReadOnlyList<UserStatusTypeResponse>>
{
    public Task<IReadOnlyList<UserStatusTypeResponse>> HandleAsync(
        ListUserStatusTypesQuery query,
        CancellationToken ct = default
    )
    {
        return cache.GetCatalogAsync(
            executor,
            "users:status-types",
            () =>
                userStatusTypes
                    .Query()
                    .OrderBy(type => type.Name)
                    .Select(Projections.UserStatusType),
            ct
        );
    }
}
