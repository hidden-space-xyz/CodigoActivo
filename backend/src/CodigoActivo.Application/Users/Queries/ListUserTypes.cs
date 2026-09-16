using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Users.Queries;

/// <summary>
/// Carries the criteria used to list user types.
/// </summary>
public sealed record ListUserTypesQuery : IQuery<IReadOnlyList<UserTypeResponse>>;

/// <summary>
/// Executes the query to list user types.
/// </summary>
/// <param name="userTypes">Repository used to persist and retrieve user types.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class ListUserTypesQueryHandler(
    IUserTypeRepository userTypes,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<ListUserTypesQuery, IReadOnlyList<UserTypeResponse>>
{
    /// <summary>
    /// Handles the request to list user types.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching user type items.</returns>
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
