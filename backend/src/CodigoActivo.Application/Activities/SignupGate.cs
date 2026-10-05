using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Checks that the signup of the event of an activity is open for the people signing up, following
/// <see cref="Event.SignupPhaseAt"/> and <see cref="EarlySignup"/>, and that the activity has not
/// started yet. Administrators are not bound by it.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class SignupGate(IEventRepository events, IUserRepository users, IClock clock)
{
    /// <summary>
    /// Ensures the people may sign up to the activity right now.
    /// </summary>
    /// <param name="activity">Activity they sign up to.</param>
    /// <param name="userIds">Identifiers of the people whose entitlement counts.</param>
    /// <param name="isAdmin">Whether an administrator acts.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> EnsureSignupOpenAsync(
        Activity activity,
        IReadOnlyList<Guid> userIds,
        bool isAdmin,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (isAdmin)
        {
            return Result.Success();
        }

        var now = clock.UtcNow;
        var ev = await events.GetByIdAsync(activity.EventId, ct);
        var phase = ev?.SignupPhaseAt(now) ?? SignupPhase.Closed;
        if (phase is SignupPhase.Closed)
        {
            return Error.Validation(ErrorCode.ActivitySignupClosed);
        }

        if (activity.HasStartedBy(now))
        {
            return Error.Validation(ErrorCode.ActivityAlreadyStarted);
        }

        return phase is SignupPhase.Open || await AllEntitledToEarlySignupAsync(userIds, ct)
            ? Result.Success()
            : Error.Validation(ErrorCode.ActivitySignupEarlyOnly);
    }

    private async Task<bool> AllEntitledToEarlySignupAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken ct
    )
    {
        var people = await users.ListByIdsAsync(userIds, ct);
        var guardianIds = people
            .Where(person => person.ParentId is not null)
            .Select(person => person.ParentId!.Value)
            .Distinct()
            .ToList();
        var guardians = guardianIds.Count is 0
            ? []
            : (await users.ListByIdsAsync(guardianIds, ct)).ToDictionary(user => user.Id);

        return people.All(person =>
            EarlySignup.IsEntitled(
                person,
                person.ParentId is { } parentId ? guardians.GetValueOrDefault(parentId) : null
            )
        );
    }
}
