using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to retrieve activity by identifier.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
public sealed record GetActivityByIdQuery(ActivityId ActivityId) : IQuery<Result<ActivityResponse>>;

/// <summary>
/// Executes the query to retrieve activity by identifier.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetActivityByIdQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetActivityByIdQuery, Result<ActivityResponse>>
{
    /// <summary>
    /// Handles the request to retrieve activity by identifier.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an activity on success, or an application error on failure.</returns>
    public async Task<Result<ActivityResponse>> HandleAsync(
        GetActivityByIdQuery query,
        CancellationToken ct = default
    )
    {
        var response = await executor.FirstOrDefaultAsync(
            readStore
                .Activities.Where(a => a.Id == query.ActivityId.Value)
                .Select(ActivityProjections.Activity),
            ct
        );
        return response is null ? Error.NotFound(ApplicationErrorCode.ActivityNotFound) : response;
    }
}
