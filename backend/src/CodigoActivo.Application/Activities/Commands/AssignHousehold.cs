using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to assign household.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="ActingUserId">Identifier of the acting user.</param>
/// <param name="Request">Validated client request data.</param>
/// <param name="IsAdmin">Whether admin.</param>
public sealed record AssignHouseholdCommand(
    Guid ActivityId,
    Guid ActingUserId,
    AssignHouseholdRequest Request,
    bool IsAdmin
) : ICommand<Result<IReadOnlyList<Guid>>>;

/// <summary>
/// Executes the command to assign household.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="signupGate">The signup gate value.</param>
/// <param name="termsGate">The terms gate value.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class AssignHouseholdCommandHandler(
    IActivityRepository activities,
    IUserRepository users,
    SignupGate signupGate,
    TermsGate termsGate,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<AssignHouseholdCommand, Result<IReadOnlyList<Guid>>>
{
    /// <summary>
    /// Handles the request to assign household.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifiers of the newly assigned users, or an application error on failure.</returns>
    public async Task<Result<IReadOnlyList<Guid>>> HandleAsync(
        AssignHouseholdCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;
        if (request.Assignments is null || request.Assignments.Count is 0)
        {
            return Error.Validation(ErrorCode.ActivityHouseholdAssignmentsRequired);
        }

        var activity = await activities.GetByIdAsync(command.ActivityId, ct);
        if (activity is null)
        {
            return Error.NotFound(ErrorCode.ActivityNotFound);
        }

        var signup = await signupGate.EnsureSignupOpenAsync(
            activity,
            [command.ActingUserId],
            command.IsAdmin,
            ct
        );
        if (signup.IsFailure)
        {
            return signup.Error!;
        }

        var items = request.Assignments.DistinctBy(a => a.UserId).ToList();
        var userIds = items.ConvertAll(item => item.UserId);
        var memberById = (await users.ListByIdsAsync(userIds, ct)).ToDictionary(u => u.Id);
        var outsideHousehold = userIds.Exists(id =>
            id != command.ActingUserId
            && (
                !memberById.TryGetValue(id, out var member)
                || !member.IsDependentOf(command.ActingUserId)
            )
        );
        if (outsideHousehold)
        {
            return Error.Forbidden(ErrorCode.ActivityHouseholdMemberNotAllowed);
        }

        if (
            items.Exists(item =>
                !memberById.TryGetValue(item.UserId, out var member)
                || !SignupRoles.Allows(member.UserTypeId, item.ActivityRoleTypeId)
            )
        )
        {
            return Error.Validation(ErrorCode.ActivityRoleNotAllowed);
        }

        var terms = await termsGate.EnsureDecidedAsync(
            activity.EventId,
            command.ActingUserId,
            request.TermsDecisions,
            ct
        );
        if (terms.IsFailure)
        {
            return terms.Error!;
        }

        var created = items
            .Where(item =>
                activity
                    .RequestAssignment(item.UserId, item.ActivityRoleTypeId, clock.UtcNow)
                    .IsSuccess
            )
            .Select(item => item.UserId)
            .ToList();

        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException ex)
            when (ex.EntityType == typeof(ActivityUserRoleAssignment))
        {
            return Error.Conflict(ErrorCode.ActivityAssignmentAlreadyExists);
        }

        if (created.Count > 0)
        {
            await cacheInvalidator.InvalidateAsync(CacheTags.Activities);
        }

        return Result.Success<IReadOnlyList<Guid>>(created);
    }
}
