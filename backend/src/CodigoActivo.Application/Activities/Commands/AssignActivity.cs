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
) : ICommand<Result>;

/// <summary>
/// Executes the command to assign activity.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="signupGate">The signup gate value.</param>
/// <param name="termsGate">The terms gate value.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class AssignActivityCommandHandler(
    IActivityRepository activities,
    IUserRepository users,
    SignupGate signupGate,
    TermsGate termsGate,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<AssignActivityCommand, Result>
{
    /// <summary>
    /// Handles the request to assign activity.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        AssignActivityCommand command,
        CancellationToken ct = default
    )
    {
        var activity = await activities.GetByIdAsync(command.ActivityId, ct);
        if (activity is null)
        {
            return Error.NotFound(ErrorCode.ActivityNotFound);
        }

        var signup = await signupGate.EnsureSignupOpenAsync(
            activity,
            [command.UserId],
            command.IsAdmin,
            ct
        );
        if (signup.IsFailure)
        {
            return signup.Error!;
        }

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (!SignupRoles.Allows(user.UserTypeId, command.Request.ActivityRoleTypeId))
        {
            return Error.Validation(ErrorCode.ActivityRoleNotAllowed);
        }

        if (activity.AssignmentOf(command.UserId) is not null)
        {
            return Error.Conflict(ErrorCode.ActivityAssignmentAlreadyExists);
        }

        if (!command.IsAdmin || command.ActingUserId == command.UserId)
        {
            var terms = await termsGate.EnsureDecidedAsync(
                activity.EventId,
                command.ActingUserId,
                command.Request.TermsDecisions,
                ct
            );
            if (terms.IsFailure)
            {
                return terms.Error!;
            }
        }

        var assigned = activity.RequestAssignment(
            command.UserId,
            command.Request.ActivityRoleTypeId,
            clock.UtcNow
        );
        if (assigned.IsFailure)
        {
            return assigned.Error!;
        }

        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException ex)
            when (ex.EntityType == typeof(ActivityUserRoleAssignment))
        {
            return Error.Conflict(ErrorCode.ActivityAssignmentAlreadyExists);
        }

        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);
        return Result.Success();
    }
}
