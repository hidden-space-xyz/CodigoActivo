using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Commands;

public sealed record ChangeAssignmentStatusCommand(
    Guid ActivityId,
    Guid UserId,
    ChangeAssignmentStatusRequest Request
) : ICommand<Result<AssignmentResponse>>;

public sealed class ChangeAssignmentStatusCommandHandler(
    IActivityRepository activities,
    IAssignmentStatusTypeRepository statuses,
    ActivitySignupNotifier notifier,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<ChangeAssignmentStatusCommand, Result<AssignmentResponse>>
{
    public async Task<Result<AssignmentResponse>> HandleAsync(
        ChangeAssignmentStatusCommand command,
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

        var status = await statuses.FindAsync(s => s.Id == command.Request.AssignmentStatusId, ct);
        if (status is null)
        {
            return Error.NotFound(ErrorCode.AssignmentStatusTypeNotFound);
        }

        var previousStatusId = assignment.AssignmentStatusId;
        assignment.AssignmentStatusId = status.Id;
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);

        if (previousStatusId != status.Id && SignupPolicy.IsDecision(status.Id))
        {
            await notifier.NotifyDecisionAsync(
                command.ActivityId,
                command.UserId,
                status.Id,
                assignment.ActivityRoleTypeId,
                ct
            );
        }

        return new AssignmentResponse(
            command.UserId,
            command.ActivityId,
            assignment.ActivityRoleTypeId,
            assignment.ActivityRoleType?.Name,
            new AssignmentStatusResponse(status.Id, status.Name)
        );
    }
}
