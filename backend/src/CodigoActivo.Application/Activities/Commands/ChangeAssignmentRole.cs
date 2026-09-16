using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to change assignment role.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record ChangeAssignmentRoleCommand(
    Guid ActivityId,
    Guid UserId,
    ChangeAssignmentRoleRequest Request
) : ICommand<Result<AssignmentResponse>>;

/// <summary>
/// Executes the command to change assignment role.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="roleTypes">Repository used to persist and retrieve role types.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class ChangeAssignmentRoleCommandHandler(
    IActivityRepository activities,
    IActivityRoleTypeRepository roleTypes,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<ChangeAssignmentRoleCommand, Result<AssignmentResponse>>
{
    /// <summary>
    /// Handles the request to change assignment role.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an assignment on success, or an application error on failure.</returns>
    public async Task<Result<AssignmentResponse>> HandleAsync(
        ChangeAssignmentRoleCommand command,
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

        var role = await roleTypes.FindAsync(r => r.Id == command.Request.ActivityRoleTypeId, ct);
        if (role is null)
        {
            return Error.NotFound(ErrorCode.ActivityRoleTypeNotFound);
        }

        var statusId = assignment.AssignmentStatusId;
        var statusName = assignment.AssignmentStatus?.Name ?? string.Empty;

        if (assignment.ActivityRoleTypeId != role.Id)
        {
            activities.RemoveAssignment(assignment);
            await activities.AddAssignmentAsync(
                new ActivityUserRoleAssignment
                {
                    UserId = command.UserId,
                    ActivityId = command.ActivityId,
                    ActivityRoleTypeId = role.Id,
                    AssignmentStatusId = statusId,
                    CreatedAt = assignment.CreatedAt,
                },
                ct
            );
            await uow.SaveChangesAsync(ct);
            await cacheInvalidator.InvalidateAsync(CacheTags.Activities);
        }

        return new AssignmentResponse(
            command.UserId,
            command.ActivityId,
            role.Id,
            role.Name,
            new AssignmentStatusResponse(statusId, statusName)
        );
    }
}
