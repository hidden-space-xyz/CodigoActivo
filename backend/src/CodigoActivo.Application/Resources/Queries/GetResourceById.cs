using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Resources.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Resources;

namespace CodigoActivo.Application.Resources.Queries;

/// <summary>
/// Carries the criteria used to retrieve resource by identifier.
/// </summary>
/// <param name="ResourceId">Identifier of the resource.</param>
public sealed record GetResourceByIdQuery(ResourceId ResourceId) : IQuery<Result<ResourceResponse>>;

/// <summary>
/// Executes the query to retrieve resource by identifier.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetResourceByIdQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetResourceByIdQuery, Result<ResourceResponse>>
{
    /// <summary>
    /// Handles the request to retrieve resource by identifier.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a resource on success, or an application error on failure.</returns>
    public async Task<Result<ResourceResponse>> HandleAsync(
        GetResourceByIdQuery query,
        CancellationToken ct = default
    )
    {
        var response = await executor.FirstOrDefaultAsync(
            readStore
                .Resources.Where(r => r.Id == query.ResourceId.Value)
                .Select(ResourceProjections.Resource),
            ct
        );
        return response is null ? Error.NotFound(ApplicationErrorCode.ResourceNotFound) : response;
    }
}
