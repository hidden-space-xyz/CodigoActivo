using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Activities.Signups;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.TermsDecisions;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to sign a person up to an activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the person signed up.</param>
/// <param name="Role">Role requested.</param>
/// <param name="TermsDecisions">
/// Decisions on the terms documents of the event for the person signed up, if any were asked.
/// </param>
public sealed record AssignActivityCommand(
    ActivityId ActivityId,
    UserId UserId,
    ActivityRole Role,
    [property: MaxLength(50)] IReadOnlyList<TermsDecision>? TermsDecisions
) : ICommand<Result>;

/// <summary>
/// Executes the command to sign a person up to an activity. The signed-in user may sign up
/// themselves or one of their dependents; an administrator may sign up anyone, even outside the
/// signup window.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="actingUser">Policy that decides for whom the signed-in user may act.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="signupGate">Gate that checks the signup window.</param>
/// <param name="termsGate">Gate that records and checks the terms decisions.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class AssignActivityCommandHandler(
    IActivityRepository activities,
    IUserRepository users,
    ActingUserPolicy actingUser,
    ICurrentUser currentUser,
    SignupGate signupGate,
    TermsGate termsGate,
    IClock clock,
    IUnitOfWork uow
) : ICommandHandler<AssignActivityCommand, Result>
{
    /// <summary>
    /// Handles the request to sign a person up to an activity.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        AssignActivityCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var allowed = await actingUser.EnsureMayActForAsync(command.UserId, ct);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        var activity = await activities.GetByIdAsync(command.ActivityId, ct);
        if (activity is null)
        {
            return Error.NotFound(ApplicationErrorCode.ActivityNotFound);
        }

        var signup = await signupGate.EnsureSignupOpenAsync(
            activity,
            [command.UserId],
            currentUser.IsAdmin,
            ct
        );
        if (signup.IsFailure)
        {
            return signup.Error!;
        }

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        var allowedRole = SignupEligibility.EnsureRoleAllowed(user, command.Role);
        if (allowedRole.IsFailure)
        {
            return allowedRole;
        }

        var free = activity.EnsureMaySignUp(command.UserId);
        if (free.IsFailure)
        {
            return free;
        }

        if (!currentUser.IsAdmin || currentUser.RequiredId() == command.UserId)
        {
            var terms = await termsGate.EnsureDecidedAsync(
                activity.EventId,
                [new PersonTermsDecisions(user, command.TermsDecisions)],
                currentUser.RequiredId(),
                ct
            );
            if (terms.IsFailure)
            {
                return terms.Error!;
            }
        }

        var assigned = activity.RequestAssignment(command.UserId, command.Role, clock.UtcNow);
        if (assigned.IsFailure)
        {
            return assigned;
        }

        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException ex) when (ex.EntityType == typeof(Assignment))
        {
            return Error.Conflict(DomainErrorCode.ActivityAssignmentAlreadyExists);
        }

        return Result.Success();
    }
}
