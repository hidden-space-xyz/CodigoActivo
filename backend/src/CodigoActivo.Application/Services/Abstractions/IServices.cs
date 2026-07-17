using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Services.Abstractions;

public interface IAuthService
{
    Task<Result<UserResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<Result<UserResponse>> GetCurrentAsync(Guid userId, CancellationToken ct = default);

    Task<Result<RegisterResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default
    );

    Task<Result<UserResponse>> VerifyAsync(Guid id, string otp, CancellationToken ct = default);

    Task<Result> ResendVerificationAsync(Guid id, CancellationToken ct = default);

    Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);

    Task<Result> ResetPasswordAsync(
        Guid id,
        ResetPasswordRequest request,
        CancellationToken ct = default
    );
}

public interface IUserService
{
    Task<PagedResult<UserResponse>> ListAsync(
        UserListQuery query,
        Guid callerId,
        bool isAdmin,
        CancellationToken ct = default
    );

    Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<UserResponse>> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken ct = default
    );

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<Result> SetAdminAsync(Guid id, bool isAdmin, CancellationToken ct = default);

    Task<Result<UserResponse>> ChangeTypeAsync(
        Guid id,
        Guid userTypeId,
        CancellationToken ct = default
    );

    Task<Result<UserResponse>> AddChildAsync(
        Guid parentId,
        RegisterMinorRequest request,
        CancellationToken ct = default
    );

    Task<Result> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<UserStatusTypeResponse>> ListStatusTypesAsync(
        CancellationToken ct = default
    );
    Task<IReadOnlyList<UserTypeResponse>> ListUserTypesAsync(CancellationToken ct = default);
}

public interface IEventService
{
    Task<PagedResult<EventListItemResponse>> ListAsync(
        EventListQuery query,
        CancellationToken ct = default
    );

    Task<Result<EventResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<int>> GetPastYearsAsync(CancellationToken ct = default);

    Task<Result<EventResponse>> CreateAsync(
        CreateEventRequest request,
        Guid userId,
        CancellationToken ct = default
    );

    Task<Result<EventResponse>> UpdateAsync(
        Guid id,
        UpdateEventRequest request,
        Guid userId,
        CancellationToken ct = default
    );

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result<EventResponse>> SetFeaturedAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<EventCategoryTypeResponse>> ListCategoryTypesAsync(
        EventCategoryTypeListQuery query,
        CancellationToken ct = default
    );

    Task<Result<EventCategoryTypeResponse>> CreateCategoryTypeAsync(
        CreateEventCategoryTypeRequest request,
        CancellationToken ct = default
    );

    Task<Result<EventCategoryTypeResponse>> UpdateCategoryTypeAsync(
        Guid id,
        UpdateEventCategoryTypeRequest request,
        CancellationToken ct = default
    );

    Task<Result> DeleteCategoryTypeAsync(Guid id, CancellationToken ct = default);
}

public interface IActivityService
{
    Task<PagedResult<ActivityResponse>> ListAsync(
        ActivityListQuery query,
        CancellationToken ct = default
    );

    Task<Result<ActivityResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<AssignedActivityResponse>> ListAssignedAsync(
        Guid userId,
        Guid? eventId = null,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<ActivityRoleTypeResponse>> ListRoleTypesAsync(
        CancellationToken ct = default
    );
    Task<IReadOnlyList<AssignmentStatusTypeResponse>> ListAssignmentStatusTypesAsync(
        CancellationToken ct = default
    );
    Task<IReadOnlyList<ActivityModalityTypeResponse>> ListModalityTypesAsync(
        CancellationToken ct = default
    );

    Task<Result<ActivityResponse>> CreateAsync(
        Guid eventId,
        CreateActivityRequest request,
        Guid userId,
        CancellationToken ct = default
    );

    Task<Result<ActivityResponse>> UpdateAsync(
        Guid activityId,
        UpdateActivityRequest request,
        Guid userId,
        CancellationToken ct = default
    );

    Task<Result> DeleteAsync(Guid activityId, CancellationToken ct = default);

    Task<Result<AssignmentResponse>> AssignAsync(
        Guid activityId,
        Guid userId,
        AssignRequest request,
        bool isAdmin,
        CancellationToken ct = default
    );

    Task<Result<IReadOnlyList<AssignmentResponse>>> AssignHouseholdAsync(
        Guid activityId,
        Guid actingUserId,
        AssignHouseholdRequest request,
        bool isAdmin,
        CancellationToken ct = default
    );

    Task<Result> UnassignAsync(
        Guid activityId,
        Guid userId,
        bool isAdmin,
        CancellationToken ct = default
    );

    Task<Result<AssignmentResponse>> ChangeStatusAsync(
        Guid activityId,
        Guid userId,
        ChangeAssignmentStatusRequest request,
        CancellationToken ct = default
    );

    Task<Result<AssignmentResponse>> ChangeRoleAsync(
        Guid activityId,
        Guid userId,
        ChangeAssignmentRoleRequest request,
        CancellationToken ct = default
    );

    Task<Result<TimeOverlapResponse>> VerifyTimeOverlapsAsync(
        Guid activityId,
        Guid userId,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<HouseholdMemberAssignmentResponse>> GetHouseholdAssignmentsAsync(
        Guid actingUserId,
        Guid eventId,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<HouseholdSignupRolesResponse>> GetHouseholdSignupRolesAsync(
        Guid actingUserId,
        CancellationToken ct = default
    );
}

public interface IResourceService
{
    Task<PagedResult<ResourceListItemResponse>> ListAsync(
        ResourceListQuery query,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<ResourceTypeResponse>> ListTypesAsync(CancellationToken ct = default);

    Task<Result<ResourceResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<ResourceResponse>> CreateAsync(
        CreateResourceRequest request,
        Guid userId,
        CancellationToken ct = default
    );

    Task<Result<ResourceResponse>> UpdateAsync(
        Guid id,
        UpdateResourceRequest request,
        Guid userId,
        CancellationToken ct = default
    );

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IAnnouncementService
{
    Task<PagedResult<AnnouncementListItemResponse>> ListAsync(
        AnnouncementListQuery query,
        CancellationToken ct = default
    );

    Task<Result<AnnouncementResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<int>> GetYearsAsync(CancellationToken ct = default);

    Task<Result<AnnouncementResponse>> CreateAsync(
        CreateAnnouncementRequest request,
        Guid userId,
        CancellationToken ct = default
    );

    Task<Result<AnnouncementResponse>> UpdateAsync(
        Guid id,
        UpdateAnnouncementRequest request,
        Guid userId,
        CancellationToken ct = default
    );

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result<AnnouncementResponse>> SetFeaturedAsync(Guid id, CancellationToken ct = default);
}

public interface IPartnerService
{
    Task<PagedResult<PartnerResponse>> ListAsync(
        PartnerListQuery query,
        CancellationToken ct = default
    );

    Task<Result<PartnerResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<PartnerResponse>> CreateAsync(
        CreatePartnerRequest request,
        Guid userId,
        CancellationToken ct = default
    );

    Task<Result<PartnerResponse>> UpdateAsync(
        Guid id,
        UpdatePartnerRequest request,
        Guid userId,
        CancellationToken ct = default
    );

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IFileService
{
    Task<Result<FileResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<FileContentValueObject>> GetContentAsync(Guid id, CancellationToken ct = default);

    Task<Result<FileResponse>> CreateAsync(
        FileUploadRequest? upload,
        Guid userId,
        CancellationToken ct = default
    );

    Task<Result<FileResponse>> UpdateAsync(
        Guid id,
        FileUploadRequest? upload,
        CancellationToken ct = default
    );

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    Task DeleteIfOrphanedAsync(Guid fileId, CancellationToken ct = default);

    Task DeleteOrphanedAsync(IReadOnlyCollection<Guid> fileIds, CancellationToken ct = default);
}

public interface IReportService
{
    Task<Result<EventSummaryResponse>> GetEventSummaryAsync(
        Guid eventId,
        CancellationToken ct = default
    );

    Task<PagedResult<EventAttendeeResponse>> ListEventAttendeesAsync(
        Guid eventId,
        EventAttendeeListQuery query,
        CancellationToken ct = default
    );

    Task<Result<EventBadgesResponse>> GetEventBadgesAsync(
        Guid eventId,
        CancellationToken ct = default
    );

    Task<Result<EventRosterResponse>> GetEventRosterAsync(
        Guid eventId,
        CancellationToken ct = default
    );

    Task<DashboardSummaryResponse> GetDashboardSummaryAsync(CancellationToken ct = default);

    Task<DashboardAnalyticsResponse> GetDashboardAnalyticsAsync(
        DashboardAnalyticsQuery query,
        CancellationToken ct = default
    );
}

public interface ISitemapService
{
    Task<string> GetSitemapXmlAsync(CancellationToken ct = default);

    string GetRobotsTxt();
}
