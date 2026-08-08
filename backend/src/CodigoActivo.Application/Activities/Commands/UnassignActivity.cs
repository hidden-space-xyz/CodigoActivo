using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Commands;

public sealed record UnassignActivityCommand(Guid ActivityId, Guid UserId, bool IsAdmin)
    : ICommand<Result>;

public sealed class UnassignActivityCommandHandler(
    IActivityRepository activities,
    SignupGate signupGate,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UnassignActivityCommand, Result>
{
    public async Task<Result> HandleAsync(
        UnassignActivityCommand command,
        CancellationToken ct = default
    )
    {
        var assignment = await activities.GetAssignmentAsync(
            command.UserId,
            command.ActivityId,
            ct
        );
        if (assignment is null)
        {
            return Error.NotFound(ErrorCode.ActivityAssignmentNotFound);
        }

        if (!command.IsAdmin)
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
        }

        activities.RemoveAssignment(assignment);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);
        return Result.Success();
    }
}
