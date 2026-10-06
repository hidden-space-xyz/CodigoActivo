using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to retrieve the assignment of a user to an activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the assigned user.</param>
public sealed record GetAssignmentQuery(ActivityId ActivityId, UserId UserId)
    : IQuery<Result<AssignmentResponse>>;

/// <summary>
/// Executes the query to retrieve the assignment of a user to an activity.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetAssignmentQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetAssignmentQuery, Result<AssignmentResponse>>
{
    /// <summary>
    /// Handles the request to retrieve the assignment of a user to an activity.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the assignment, or an application error when it does not exist.</returns>
    public async Task<Result<AssignmentResponse>> HandleAsync(
        GetAssignmentQuery query,
        CancellationToken ct = default
    )
    {
        var response = await executor.FirstOrDefaultAsync(
            readStore
                .Assignments.Where(a =>
                    a.ActivityId == query.ActivityId.Value && a.UserId == query.UserId.Value
                )
                .Select(ActivityProjections.Assignment),
            ct
        );
        return response is null
            ? Error.NotFound(DomainErrorCode.ActivityAssignmentNotFound)
            : response;
    }
}
