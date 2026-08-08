using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities;

public sealed class SignupGate(
    IActivityRepository activities,
    IUserRepository users,
    IQueryExecutor executor,
    IClock clock
)
{
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
