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
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to sign members of the signed-in user's household up to an activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Assignments">Members to sign up with their roles and the terms decisions taken for them.</param>
public sealed record AssignHouseholdCommand(
    ActivityId ActivityId,
    [property: MaxLength(Household.MaxMembers)] IReadOnlyList<HouseholdMemberSignup> Assignments
) : ICommand<Result<IReadOnlyList<UserId>>>;

/// <summary>
/// Executes the command to sign members of the signed-in user's household up to an activity.
/// Every member must belong to the household (<see cref="Household.Includes"/>) and may take the
/// role asked for; members already signed up are left as they are, and the result lists those
/// signed up now. The required terms documents of the event must end up accepted for each member
/// signed up now.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="signupGate">Gate that checks the signup window.</param>
/// <param name="termsGate">Gate that records and checks the terms decisions.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class AssignHouseholdCommandHandler(
    IActivityRepository activities,
    IUserRepository users,
    ICurrentUser currentUser,
    SignupGate signupGate,
    TermsGate termsGate,
    IClock clock,
    IUnitOfWork uow
) : ICommandHandler<AssignHouseholdCommand, Result<IReadOnlyList<UserId>>>
{
    /// <summary>
    /// Handles the request to sign household members up to an activity.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifiers of the members signed up, or an application error on failure.</returns>
    public async Task<Result<IReadOnlyList<UserId>>> HandleAsync(
        AssignHouseholdCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Assignments.Count is 0)
        {
            return Error.Validation(ApplicationErrorCode.ActivityHouseholdAssignmentsRequired);
        }

        var activity = await activities.GetByIdAsync(command.ActivityId, ct);
        if (activity is null)
        {
            return Error.NotFound(ApplicationErrorCode.ActivityNotFound);
        }

        var actingUserId = currentUser.RequiredId();
        var signup = await signupGate.EnsureSignupOpenAsync(
            activity,
            [actingUserId],
            currentUser.IsAdmin,
            ct
        );
        if (signup.IsFailure)
        {
            return signup.Error!;
        }

        var items = command.Assignments.DistinctBy(a => a.UserId).ToList();
        var memberById = (
            await users.ListByIdsAsync(items.ConvertAll(item => item.UserId), ct)
        ).ToDictionary(u => u.Id);
        if (
            items.Exists(item =>
                !memberById.TryGetValue(item.UserId, out var member)
                || !Household.Includes(actingUserId, member)
            )
        )
        {
            return Error.Forbidden(ApplicationErrorCode.ActivityHouseholdMemberNotAllowed);
        }

        foreach (var item in items)
        {
            var allowedRole = SignupEligibility.EnsureRoleAllowed(
                memberById[item.UserId],
                item.Role
            );
            if (allowedRole.IsFailure)
            {
                return allowedRole.Error!;
            }
        }

        var pending = items.FindAll(item => activity.EnsureMaySignUp(item.UserId).IsSuccess);
        var terms = await termsGate.EnsureDecidedAsync(
            activity.EventId,
            [
                .. pending.Select(item => new PersonTermsDecisions(
                    memberById[item.UserId],
                    item.TermsDecisions
                )),
            ],
            actingUserId,
            ct
        );
        if (terms.IsFailure)
        {
            return terms.Error!;
        }

        var created = new List<UserId>(pending.Count);
        foreach (var item in pending)
        {
            var assigned = activity.RequestAssignment(item.UserId, item.Role, clock.UtcNow);
            if (assigned.IsFailure)
            {
                return assigned.Error!;
            }

            created.Add(item.UserId);
        }

        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException ex) when (ex.EntityType == typeof(Assignment))
        {
            return Error.Conflict(DomainErrorCode.ActivityAssignmentAlreadyExists);
        }

        return Result.Success<IReadOnlyList<UserId>>(created);
    }
}
