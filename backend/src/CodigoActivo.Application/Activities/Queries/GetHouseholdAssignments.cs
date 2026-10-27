using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to retrieve the signups of the signed-in user's household to an event.
/// </summary>
/// <param name="EventId">Identifier of the event whose activities are read.</param>
public sealed record GetHouseholdAssignmentsQuery(EventId EventId)
    : IQuery<Result<IReadOnlyList<HouseholdMemberAssignmentResponse>>>;

/// <summary>
/// Executes the query to retrieve the signups of the signed-in user and their dependents to the
/// activities of an event.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="currentUser">Person the query runs for.</param>
public sealed class GetHouseholdAssignmentsQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    ICurrentUser currentUser
)
    : IQueryHandler<
        GetHouseholdAssignmentsQuery,
        Result<IReadOnlyList<HouseholdMemberAssignmentResponse>>
    >
{
    /// <summary>
    /// Handles the request to retrieve household assignments.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the signups ordered by member name and activity start, or
    /// <see cref="ApplicationErrorCode.EventNotFound"/>.
    /// </returns>
    public async Task<Result<IReadOnlyList<HouseholdMemberAssignmentResponse>>> HandleAsync(
        GetHouseholdAssignmentsQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var eventId = query.EventId.Value;
        var actingUserId = currentUser.RequiredId().Value;
        if (!await executor.AnyAsync(readStore.Events.Where(e => e.Id == eventId), ct))
        {
            return Error.NotFound(ApplicationErrorCode.EventNotFound);
        }

        var assignments = await executor.ToListAsync(
            readStore
                .Assignments.Where(x =>
                    x.Activity.EventId == eventId
                    && (x.UserId == actingUserId || x.User.ParentId == actingUserId)
                )
                .OrderBy(x => x.User.FirstName)
                .ThenBy(x => x.User.LastName)
                .ThenBy(x => x.Activity.ActivityStartsAt)
                .ThenBy(x => x.ActivityId)
                .Select(x => new HouseholdMemberAssignmentResponse(
                    x.ActivityId,
                    x.UserId,
                    x.User.FirstName,
                    x.User.LastName,
                    x.ActivityRoleTypeId,
                    x.ActivityRoleType.Name,
                    x.AssignmentStatusId,
                    x.AssignmentStatus.Name
                )),
            ct
        );
        return Result.Success<IReadOnlyList<HouseholdMemberAssignmentResponse>>(assignments);
    }
}
