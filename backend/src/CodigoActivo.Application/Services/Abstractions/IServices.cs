using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Services.Abstractions;

public interface IAuthService
{
    public Task<Result<UserResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    public Task<Result<UserResponse>> GetCurrentAsync(Guid userId, CancellationToken ct = default);

    public Task<Result<RegisterResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default
    );

    public Task<Result<UserResponse>> VerifyAsync(Guid id, string otp, CancellationToken ct = default);

    public Task<Result> ResendVerificationAsync(Guid id, CancellationToken ct = default);

    public Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);

    public Task<Result> ResetPasswordAsync(
        Guid id,
        ResetPasswordRequest request,
        CancellationToken ct = default
    );
}

public interface IUserService
{
    public Task<PagedResult<UserResponse>> ListAsync(
        UserListQuery query,
        Guid callerId,
        bool isAdmin,
        CancellationToken ct = default
    );

    public Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);

    public Task<Result<UserResponse>> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken ct = default
    );

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    public Task<Result> SetAdminAsync(Guid id, bool isAdmin, CancellationToken ct = default);

    public Task<Result<UserResponse>> ChangeTypeAsync(
        Guid id,
        Guid userTypeId,
        CancellationToken ct = default
    );

    public Task<Result<UserResponse>> AddChildAsync(
        Guid parentId,
        RegisterMinorRequest request,
        CancellationToken ct = default
    );

    public Task<Result> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken ct = default
    );

    public Task<IReadOnlyList<UserStatusTypeResponse>> ListStatusTypesAsync(
        CancellationToken ct = default
    );
    public Task<IReadOnlyList<UserTypeResponse>> ListUserTypesAsync(CancellationToken ct = default);
}

public interface IEventService
{
    public Task<PagedResult<EventListItemResponse>> ListAsync(
        EventListQuery query,
        CancellationToken ct = default
    );

    public Task<Result<EventResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);

    public Task<IReadOnlyList<int>> GetPastYearsAsync(CancellationToken ct = default);

    public Task<Result<EventResponse>> CreateAsync(
        CreateEventRequest request,
        Guid userId,
        CancellationToken ct = default
    );

    public Task<Result<EventResponse>> UpdateAsync(
        Guid id,
        UpdateEventRequest request,
        Guid userId,
        CancellationToken ct = default
    );

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    public Task<Result<EventResponse>> SetFeaturedAsync(Guid id, CancellationToken ct = default);

    public Task<PagedResult<EventCategoryTypeResponse>> ListCategoryTypesAsync(
        EventCategoryTypeListQuery query,
        CancellationToken ct = default
    );

    public Task<Result<EventCategoryTypeResponse>> CreateCategoryTypeAsync(
        CreateEventCategoryTypeRequest request,
        CancellationToken ct = default
    );

    public Task<Result<EventCategoryTypeResponse>> UpdateCategoryTypeAsync(
        Guid id,
        UpdateEventCategoryTypeRequest request,
        CancellationToken ct = default
    );

    public Task<Result> DeleteCategoryTypeAsync(Guid id, CancellationToken ct = default);
}

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

public interface IParticipationService
{
    public Task<IReadOnlyList<EventHistoryResponse>> GetHistoryAsync(
        Guid userId,
        CancellationToken ct = default
    );

    public Task<IReadOnlyList<EventCertificateResponse>> GetCertificatesAsync(
        Guid userId,
        CancellationToken ct = default
    );

    public Task<Result<EventRatingResponse>> SaveRatingAsync(
        Guid eventId,
        Guid userId,
        SaveEventRatingRequest request,
        CancellationToken ct = default
    );

    public Task<Result<PagedResult<EventRatingListItemResponse>>> ListEventRatingsAsync(
        Guid eventId,
        EventRatingListQuery query,
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
