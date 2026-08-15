using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Commands;

public sealed record AssignActivityCommand(
    Guid ActivityId,
    Guid UserId,
    Guid ActingUserId,
    AssignRequest Request,
    bool IsAdmin
) : ICommand<Result<AssignmentResponse>>;

public sealed class AssignActivityCommandHandler(
    IActivityRepository activities,
    IUserRepository users,
    SignupGate signupGate,
    TermsGate termsGate,
    ActivitySignupNotifier notifier,
    ListAssignmentStatusTypesQueryHandler statusTypes,
    IQueryExecutor executor,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<AssignActivityCommand, Result<AssignmentResponse>>
{
    public async Task<Result<AssignmentResponse>> HandleAsync(
        AssignActivityCommand command,
        CancellationToken ct = default
    )
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

        var userTypeId = await executor.FirstOrDefaultAsync(
            users.Query().Where(u => u.Id == command.UserId).Select(u => (Guid?)u.UserTypeId),
            ct
        );
        if (userTypeId is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (!SignupPolicy.IsSignupRoleAllowed(userTypeId.Value, command.Request.ActivityRoleTypeId))
        {
            return Error.BadRequest(ErrorCode.ActivityRoleNotAllowed);
        }

        if (await activities.AssignmentExistsAsync(command.UserId, command.ActivityId, ct))
        {
            return Error.Conflict(ErrorCode.ActivityAssignmentAlreadyExists);
        }

        if (!command.IsAdmin || command.ActingUserId == command.UserId)
        {
            var terms = await termsGate.EnsureAcceptedAsync(
                command.ActivityId,
                command.ActingUserId,
                command.Request.AcceptTerms,
                ct
            );
            if (terms.IsFailure)
            {
                return terms.Error!;
            }
        }

        var assignment = new ActivityUserRoleAssignment
        {
            UserId = command.UserId,
            ActivityId = command.ActivityId,
            ActivityRoleTypeId = command.Request.ActivityRoleTypeId,
            AssignmentStatusId = SeedIds.AssignmentStatusTypes.Requested,
            CreatedAt = clock.UtcNow,
        };
        await activities.AddAssignmentAsync(assignment, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);

        await notifier.NotifySignupAsync(
            command.ActivityId,
            command.UserId,
            [new SignupLine(command.UserId, command.Request.ActivityRoleTypeId)],
            ct
        );

        var requestedStatus = await GetRequestedStatusAsync(ct);
        return new AssignmentResponse(
            command.UserId,
            command.ActivityId,
            command.Request.ActivityRoleTypeId,
            null,
            requestedStatus
        );
    }

    private async Task<AssignmentStatusResponse> GetRequestedStatusAsync(CancellationToken ct)
    {
        var status = (
            await statusTypes.HandleAsync(new ListAssignmentStatusTypesQuery(), ct)
        ).FirstOrDefault(s => s.Id == SeedIds.AssignmentStatusTypes.Requested);
        return new AssignmentStatusResponse(
            SeedIds.AssignmentStatusTypes.Requested,
            status?.Name ?? string.Empty
        );
    }
}
