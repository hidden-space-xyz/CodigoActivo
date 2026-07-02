using CodigoActivo.Application.DTOs;
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
}

public interface IUserService
{
    IQueryable<UserResponse> QueryUsers();
    Task<Result<UserResponse>> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken ct = default
    );
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
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
    Task<Result<UserResponse>> SetRoleAsync(
        Guid userId,
        Guid roleId,
        CancellationToken ct = default
    );
    Task<Result> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken ct = default
    );

    IQueryable<UserStatusTypeResponse> QueryStatusTypes();
    IQueryable<UserTypeResponse> QueryUserTypes();

    IQueryable<RegistrationTypeResponse> QueryRegistrationTypes();
}

public interface IEventService
{
    IQueryable<EventResponse> Query();
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
}

public interface IActivityService
{
    IQueryable<ActivityResponse> QueryActivities();

    IQueryable<AssignedActivityResponse> QueryAssigned(Guid userId);

    IQueryable<ActivityRoleTypeResponse> QueryRoleTypes();
    IQueryable<AssignmentStatusTypeResponse> QueryAssignmentStatusTypes();

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

    Task<ActivityRoleTypeResponse> CreateActivityRoleTypeAsync(
        CreateActivityRoleTypeRequest request,
        CancellationToken ct = default
    );
    Task<Result<ActivityRoleTypeResponse>> UpdateActivityRoleTypeAsync(
        Guid id,
        UpdateActivityRoleTypeRequest request,
        CancellationToken ct = default
    );
    Task<Result> DeleteActivityRoleTypeAsync(Guid id, CancellationToken ct = default);
}

public interface IResourceService
{
    IQueryable<ResourceResponse> Query();
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
    IQueryable<AnnouncementResponse> Query();
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
    IQueryable<PartnerResponse> Query();
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
        FileUploadRequest upload,
        Guid userId,
        CancellationToken ct = default
    );
    Task<Result<FileResponse>> UpdateAsync(
        Guid id,
        FileUploadRequest upload,
        CancellationToken ct = default
    );
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IReportService
{
    Task<Result<EventSummaryResponse>> GetEventSummaryAsync(
        Guid eventId,
        CancellationToken ct = default
    );
    Task<Result<EventAssignmentsReportResponse>> GetEventAssignmentsAsync(
        Guid eventId,
        CancellationToken ct = default
    );
    Task<Result<ActivityAssignmentsReportResponse>> GetActivityAssignmentsAsync(
        Guid activityId,
        CancellationToken ct = default
    );
    Task<DashboardSummaryResponse> GetDashboardSummaryAsync(CancellationToken ct = default);
}
