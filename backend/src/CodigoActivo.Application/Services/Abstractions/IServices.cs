using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Services.Abstractions;

public interface IActivityService
{
    public Task<PagedResult<ActivityResponse>> ListAsync(
        ActivityListQuery query,
        CancellationToken ct = default
    );

    public Task<Result<ActivityResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);

    public Task<IReadOnlyList<AssignedActivityResponse>> ListAssignedAsync(
        Guid userId,
        Guid? eventId = null,
        CancellationToken ct = default
    );

    public Task<IReadOnlyList<ActivityRoleTypeResponse>> ListRoleTypesAsync(
        CancellationToken ct = default
    );
    public Task<IReadOnlyList<AssignmentStatusTypeResponse>> ListAssignmentStatusTypesAsync(
        CancellationToken ct = default
    );
    public Task<IReadOnlyList<ActivityModalityTypeResponse>> ListModalityTypesAsync(
        CancellationToken ct = default
    );

    public Task<Result<ActivityResponse>> CreateAsync(
        Guid eventId,
        CreateActivityRequest request,
        Guid userId,
        CancellationToken ct = default
    );

    public Task<Result<ActivityResponse>> UpdateAsync(
        Guid activityId,
        UpdateActivityRequest request,
        Guid userId,
        CancellationToken ct = default
    );

    public Task<Result> DeleteAsync(Guid activityId, CancellationToken ct = default);

    public Task<Result<AssignmentResponse>> AssignAsync(
        Guid activityId,
        Guid userId,
        AssignRequest request,
        bool isAdmin,
        CancellationToken ct = default
    );

    public Task<Result<IReadOnlyList<AssignmentResponse>>> AssignHouseholdAsync(
        Guid activityId,
        Guid actingUserId,
        AssignHouseholdRequest request,
        bool isAdmin,
        CancellationToken ct = default
    );

    public Task<Result> UnassignAsync(
        Guid activityId,
        Guid userId,
        bool isAdmin,
        CancellationToken ct = default
    );

    public Task<Result<AssignmentResponse>> ChangeStatusAsync(
        Guid activityId,
        Guid userId,
        ChangeAssignmentStatusRequest request,
        CancellationToken ct = default
    );

    public Task<Result<AssignmentResponse>> ChangeRoleAsync(
        Guid activityId,
        Guid userId,
        ChangeAssignmentRoleRequest request,
        CancellationToken ct = default
    );

    public Task<Result<TimeOverlapResponse>> VerifyTimeOverlapsAsync(
        Guid activityId,
        Guid userId,
        CancellationToken ct = default
    );

    public Task<IReadOnlyList<HouseholdMemberAssignmentResponse>> GetHouseholdAssignmentsAsync(
        Guid actingUserId,
        Guid eventId,
        CancellationToken ct = default
    );

    public Task<IReadOnlyList<HouseholdSignupRolesResponse>> GetHouseholdSignupRolesAsync(
        Guid actingUserId,
        CancellationToken ct = default
    );
}

public interface IReportService
{
    public Task<Result<EventSummaryResponse>> GetEventSummaryAsync(
        Guid eventId,
        CancellationToken ct = default
    );

    public Task<PagedResult<EventAttendeeResponse>> ListEventAttendeesAsync(
        Guid eventId,
        EventAttendeeListQuery query,
        CancellationToken ct = default
    );

    public Task<Result<EventBadgesResponse>> GetEventBadgesAsync(
        Guid eventId,
        CancellationToken ct = default
    );

    public Task<Result<EventRosterResponse>> GetEventRosterAsync(
        Guid eventId,
        CancellationToken ct = default
    );

    public Task<DashboardSummaryResponse> GetDashboardSummaryAsync(CancellationToken ct = default);

    public Task<DashboardAnalyticsResponse> GetDashboardAnalyticsAsync(
        DashboardAnalyticsQuery query,
        CancellationToken ct = default
    );
}

public interface IEmailService
{
    public Task<Result<SendEmailResultResponse>> SendToUserAsync(
        Guid userId,
        SendEmailRequest request,
        IReadOnlyList<EmailAttachmentUpload> attachments,
        CancellationToken ct = default
    );

    public Task<Result<SendEmailResultResponse>> SendToUsersAsync(
        UserListQuery query,
        SendEmailRequest request,
        IReadOnlyList<EmailAttachmentUpload> attachments,
        CancellationToken ct = default
    );

    public Task<Result<SendEmailResultResponse>> SendToEventAttendeesAsync(
        Guid eventId,
        EventAttendeeListQuery query,
        SendEmailRequest request,
        IReadOnlyList<EmailAttachmentUpload> attachments,
        CancellationToken ct = default
    );
}
