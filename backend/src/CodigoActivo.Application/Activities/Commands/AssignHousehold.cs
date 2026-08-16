using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Commands;

public sealed record AssignHouseholdCommand(
    Guid ActivityId,
    Guid ActingUserId,
    AssignHouseholdRequest Request,
    bool IsAdmin
) : ICommand<Result<IReadOnlyList<AssignmentResponse>>>;

public sealed class AssignHouseholdCommandHandler(
    IActivityRepository activities,
    IUserRepository users,
    SignupGate signupGate,
    TermsGate termsGate,
    ListAssignmentStatusTypesQueryHandler statusTypes,
    IQueryExecutor executor,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<AssignHouseholdCommand, Result<IReadOnlyList<AssignmentResponse>>>
{
    public async Task<Result<IReadOnlyList<AssignmentResponse>>> HandleAsync(
        AssignHouseholdCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        if (request.Assignments is null || request.Assignments.Count is 0)
        {
            return Error.BadRequest(ErrorCode.ActivityHouseholdAssignmentsRequired);
        }

        var signup = await signupGate.EnsureSignupOpenAsync(
            command.ActivityId,
            [command.ActingUserId],
            command.IsAdmin,
            ct
        );
        if (signup.IsFailure)
        {
            return signup.Error!;
        }

        var items = request.Assignments.DistinctBy(a => a.UserId).ToList();
        var userIds = items.ConvertAll(item => item.UserId);

        var members = await executor.ToListAsync(
            users
                .Query()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    u.UserTypeId,
                    u.ParentId,
                }),
            ct
        );
        var memberById = members.ToDictionary(u => u.Id);

        var outsideHousehold = userIds.Exists(id =>
            id != command.ActingUserId
            && (
                !memberById.TryGetValue(id, out var member)
                || member.ParentId != command.ActingUserId
            )
        );
        if (outsideHousehold)
        {
            return Error.Forbidden(ErrorCode.ActivityHouseholdMemberNotAllowed);
        }

        if (
            items.Exists(item =>
                !memberById.TryGetValue(item.UserId, out var member)
                || !SignupPolicy.IsSignupRoleAllowed(member.UserTypeId, item.ActivityRoleTypeId)
            )
        )
        {
            return Error.BadRequest(ErrorCode.ActivityRoleNotAllowed);
        }

        var terms = await termsGate.EnsureAcceptedAsync(
            command.ActivityId,
            command.ActingUserId,
            request.AcceptTerms,
            ct
        );
        if (terms.IsFailure)
        {
            return terms.Error!;
        }

        var alreadyAssigned = (
            await executor.ToListAsync(
                activities
                    .QueryAssignments()
                    .Where(x => x.ActivityId == command.ActivityId && userIds.Contains(x.UserId))
                    .Select(x => x.UserId),
                ct
            )
        ).ToHashSet();

        var requestedStatus = await GetRequestedStatusAsync(ct);
        var created = new List<AssignmentResponse>();
        foreach (var item in items)
        {
            if (alreadyAssigned.Contains(item.UserId))
            {
                continue;
            }

            await activities.AddAssignmentAsync(
                new ActivityUserRoleAssignment
                {
                    UserId = item.UserId,
                    ActivityId = command.ActivityId,
                    ActivityRoleTypeId = item.ActivityRoleTypeId,
                    AssignmentStatusId = SeedIds.AssignmentStatusTypes.Requested,
                    CreatedAt = clock.UtcNow,
                },
                ct
            );
            created.Add(
                new AssignmentResponse(
                    item.UserId,
                    command.ActivityId,
                    item.ActivityRoleTypeId,
                    null,
                    requestedStatus
                )
            );
        }

        await uow.SaveChangesAsync(ct);
        if (created.Count > 0)
        {
            await cacheInvalidator.InvalidateAsync(CacheTags.Activities);
        }

        return Result.Success<IReadOnlyList<AssignmentResponse>>(created);
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
