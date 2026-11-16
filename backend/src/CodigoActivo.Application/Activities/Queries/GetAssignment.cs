using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to retrieve the signup of a person to an activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the person signed up.</param>
public sealed record GetAssignmentQuery(ActivityId ActivityId, UserId UserId)
    : IQuery<Result<AssignmentResponse>>;

/// <summary>
/// Executes the query to retrieve the signup of a person to an activity.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetAssignmentQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetAssignmentQuery, Result<AssignmentResponse>>
{
    /// <summary>
    /// Handles the request to retrieve the signup of a person to an activity.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the signup, or
    /// <see cref="DomainErrorCode.ActivityAssignmentNotFound"/> when the person is not signed up.
    /// </returns>
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
