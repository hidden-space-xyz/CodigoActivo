using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Evaluates whether signup is allowed by the business rules.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class SignupGate(
    IActivityRepository activities,
    IUserRepository users,
    IQueryExecutor executor,
    IClock clock
)
{
    /// <summary>
    /// Ensures that signup open satisfies the required business rules.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="userIds">Identifiers of the user items.</param>
    /// <param name="isAdmin">Whether is admin.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> EnsureSignupOpenAsync(
        Guid activityId,
        IReadOnlyList<Guid> userIds,
        bool isAdmin,
        CancellationToken ct
    )
    {
        var window = await executor.FirstOrDefaultAsync(
            activities
                .Query()
                .Where(a => a.Id == activityId)
                .Select(a => new SignupWindow(
                    a.Event.EarlySignupStartsAt,
                    a.Event.SignupStartsAt,
                    a.Event.SignupEndsAt
                )),
            ct
        );
        if (window is null)
        {
            return Error.NotFound(ErrorCode.ActivityNotFound);
        }

        if (isAdmin)
        {
            return Result.Success();
        }

        var now = clock.UtcNow;
        return now switch
        {
            _ when now > window.EndsAt => Error.BadRequest(ErrorCode.ActivitySignupClosed),
            _ when now >= window.StartsAt => Result.Success(),
            _ when window.EarlyStartsAt is not { } earlyStart || now < earlyStart =>
                Error.BadRequest(ErrorCode.ActivitySignupClosed),
            _ => await AllAllowedInEarlySignupAsync(userIds, ct)
                ? Result.Success()
                : Error.BadRequest(ErrorCode.ActivitySignupEarlyOnly),
        };
    }

    private async Task<bool> AllAllowedInEarlySignupAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken ct
    )
    {
        var userTypeIds = await executor.ToListAsync(
            users
                .Query()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => u.Parent == null ? u.UserTypeId : u.Parent.UserTypeId),
            ct
        );
        return userTypeIds.All(IsEarlySignupUserType);
    }

    private static bool IsEarlySignupUserType(Guid userTypeId)
    {
        return userTypeId == SeedIds.UserTypes.Member || userTypeId == SeedIds.UserTypes.Sponsor;
    }

    private sealed record SignupWindow(
        DateTimeOffset? EarlyStartsAt,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt
    );
}
