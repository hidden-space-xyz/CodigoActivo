using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Lets <see cref="SignupEligibility"/> decide whether the signup window of an activity is open to
/// the people signing up or withdrawing. Administrators act outside the window, so nothing is
/// checked for them; for anyone else it loads the event and, only while early signup alone is open,
/// the people with their guardians.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class SignupGate(IEventRepository events, IUserRepository users, IClock clock)
{
    /// <summary>
    /// Ensures the people may sign up to the activity, or withdraw from it, right now.
    /// </summary>
    /// <param name="activity">Activity they sign up to.</param>
    /// <param name="userIds">Identifiers of the people whose entitlement counts.</param>
    /// <param name="isAdmin">Whether an administrator acts.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or the rule that refuses them.</returns>
    public async Task<Result> EnsureSignupOpenAsync(
        Activity activity,
        IReadOnlyList<UserId> userIds,
        bool isAdmin,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (isAdmin)
        {
            return Result.Success();
        }

        var ev = await events.GetByIdAsync(activity.EventId, ct);
        if (ev is null)
        {
            return Error.Validation(DomainErrorCode.ActivitySignupClosed);
        }

        var now = clock.UtcNow;
        var people =
            ev.SignupPhaseAt(now) is SignupPhase.EarlyOnly
                ? await CandidatesAsync(userIds, ct)
                : [];
        return SignupEligibility.EnsureWindowOpen(ev, activity, people, now);
    }

    private async Task<IReadOnlyList<SignupCandidate>> CandidatesAsync(
        IReadOnlyList<UserId> userIds,
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

        return
        [
            .. people.Select(person => new SignupCandidate(
                person,
                person.ParentId is { } parentId ? guardians.GetValueOrDefault(parentId) : null
            )),
        ];
    }
}
