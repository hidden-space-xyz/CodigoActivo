using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Decides who may sign up for an activity or withdraw from it, and in which role. Both follow the
/// signup window of the event and stop once the activity starts; while only early signup is open,
/// every person must be entitled to it through their own type or their guardian's.
/// </summary>
public static class SignupEligibility
{
    /// <summary>
    /// Ensures the signup window of an activity is open for some people.
    /// </summary>
    /// <param name="ev">Event the activity belongs to.</param>
    /// <param name="activity">Activity to sign up for or withdraw from.</param>
    /// <param name="people">
    /// People signing up or withdrawing, each with their guardian, if any. They only count while
    /// only early signup is open.
    /// </param>
    /// <param name="now">Current time.</param>
    /// <returns>
    /// Success when the window is open to them, or
    /// <see cref="DomainErrorCode.ActivitySignupClosed"/> when the signup window is closed,
    /// <see cref="DomainErrorCode.ActivityAlreadyStarted"/> when the activity has started, or
    /// <see cref="DomainErrorCode.ActivitySignupEarlyOnly"/> when only early signup is open and
    /// someone is not entitled to it.
    /// </returns>
    /// <exception cref="ArgumentException">The activity belongs to another event.</exception>
    public static Result EnsureWindowOpen(
        Event ev,
        Activity activity,
        IReadOnlyCollection<SignupCandidate> people,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(people);
        if (activity.EventId != ev.Id)
        {
            throw new ArgumentException("The activity belongs to another event.", nameof(activity));
        }

        var phase = ev.SignupPhaseAt(now);
        if (phase is SignupPhase.Closed)
        {
            return Error.Validation(DomainErrorCode.ActivitySignupClosed);
        }

        if (activity.HasStartedBy(now))
        {
            return Error.Validation(DomainErrorCode.ActivityAlreadyStarted);
        }

        return
            phase is SignupPhase.Open
            || people.All(candidate => EarlySignup.IsEntitled(candidate.Person, candidate.Guardian))
            ? Result.Success()
            : Error.Validation(DomainErrorCode.ActivitySignupEarlyOnly);
    }

    /// <summary>
    /// Ensures a person may take a role, which depends on the type of the person.
    /// </summary>
    /// <param name="person">Person signing up.</param>
    /// <param name="role">Role asked for.</param>
    /// <returns>
    /// Success when the type of the person allows the role, or
    /// <see cref="DomainErrorCode.ActivityRoleNotAllowed"/> when it does not.
    /// </returns>
    public static Result EnsureRoleAllowed(User person, ActivityRole role)
    {
        ArgumentNullException.ThrowIfNull(person);

        return SignupRoles.Allows(person.UserType, role)
            ? Result.Success()
            : Error.Validation(DomainErrorCode.ActivityRoleNotAllowed);
    }
}
