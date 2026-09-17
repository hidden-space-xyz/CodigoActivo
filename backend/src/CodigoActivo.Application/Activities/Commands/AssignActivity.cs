using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to assign activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="ActingUserId">Identifier of the acting user.</param>
/// <param name="Request">Validated client request data.</param>
/// <param name="IsAdmin">Whether admin.</param>
public sealed record AssignActivityCommand(
    Guid ActivityId,
    Guid UserId,
    Guid ActingUserId,
    AssignRequest Request,
    bool IsAdmin
) : ICommand<Result<AssignmentResponse>>;

/// <summary>
/// Executes the command to assign activity.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="signupGate">The signup gate value.</param>
/// <param name="termsGate">The terms gate value.</param>
/// <param name="statusTypes">Handler used to list assignment status types.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class AssignActivityCommandHandler(
    IActivityRepository activities,
    IUserRepository users,
    SignupGate signupGate,
    TermsGate termsGate,
    ListAssignmentStatusTypesQueryHandler statusTypes,
    IQueryExecutor executor,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<AssignActivityCommand, Result<AssignmentResponse>>
{
    /// <summary>
    /// Handles the request to assign activity.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an assignment on success, or an application error on failure.</returns>
    public async Task<Result<AssignmentResponse>> HandleAsync(
        AssignActivityCommand command,
        CancellationToken ct = default
    )
    {
        var signup = await signupGate.EnsureSignupOpenAsync(
            command.ActivityId,
            [command.UserId],
            command.IsAdmin,
            ct
        );
        if (signup.IsFailure)
        {
            return signup.Error!;
        }

        var userTypeId = await executor.FirstOrDefaultAsync(
            users.Query().Where(u => u.Id == command.UserId).Select(u => (Guid?)u.UserTypeId),
            ct
        );
        if (userTypeId is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (!SignupPolicy.IsSignupRoleAllowed(userTypeId.Value, command.Request.ActivityRoleTypeId))
        {
            return Error.BadRequest(ErrorCode.ActivityRoleNotAllowed);
        }

        if (await activities.AssignmentExistsAsync(command.UserId, command.ActivityId, ct))
        {
            return Error.Conflict(ErrorCode.ActivityAssignmentAlreadyExists);
        }

        if (!command.IsAdmin || command.ActingUserId == command.UserId)
        {
            var terms = await termsGate.EnsureDecidedAsync(
                command.ActivityId,
                command.ActingUserId,
                command.Request.TermsDecisions,
                ct
            );
            if (terms.IsFailure)
            {
                return terms.Error!;
            }
        }

        var assignment = new ActivityUserRoleAssignment
        {
            UserId = command.UserId,
            ActivityId = command.ActivityId,
            ActivityRoleTypeId = command.Request.ActivityRoleTypeId,
            AssignmentStatusId = SeedIds.AssignmentStatusTypes.Requested,
            CreatedAt = clock.UtcNow,
        };
        await activities.AddAssignmentAsync(assignment, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);

        var requestedStatus = await GetRequestedStatusAsync(ct);
        return new AssignmentResponse(
            command.UserId,
            command.ActivityId,
            command.Request.ActivityRoleTypeId,
            null,
            requestedStatus
        );
    }

    private async Task<AssignmentStatusResponse> GetRequestedStatusAsync(CancellationToken ct)
    {
        var status = (
            await statusTypes.HandleAsync(new ListAssignmentStatusTypesQuery(), ct)
        ).FirstOrDefault(s => s.Id == SeedIds.AssignmentStatusTypes.Requested);
        return new AssignmentStatusResponse(
            SeedIds.AssignmentStatusTypes.Requested,
            status?.Name ?? string.Empty
        );
    }
}
