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
    Task<IReadOnlyList<UserResponse>> GetAllAsync(CancellationToken ct = default);
    Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
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

    Task<IReadOnlyList<UserResponse>> GetChildrenAsync(
        Guid parentId,
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

    Task<IReadOnlyList<UserStatusTypeResponse>> GetUserStatusTypesAsync(
        CancellationToken ct = default
    );

    Task<IReadOnlyList<UserTypeResponse>> GetUserTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UserTypeResponse>> GetRegistrationTypesAsync(
        bool forMinor,
        CancellationToken ct = default
    );
}

public interface IEventService
{
    Task<IReadOnlyList<EventResponse>> ListAsync(
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken ct = default
    );
    Task<Result<EventResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
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
    Task<IReadOnlyList<ActivityResponse>> ListByEventAsync(
        Guid eventId,
        CancellationToken ct = default
    );
    Task<Result<ActivityResponse>> GetByIdAsync(
        Guid eventId,
        Guid activityId,
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
        CancellationToken ct = default
    );
    Task<Result<IReadOnlyList<AssignmentResponse>>> AssignHouseholdAsync(
        Guid activityId,
        Guid actingUserId,
        AssignHouseholdRequest request,
        CancellationToken ct = default
    );
    Task<Result> UnassignAsync(Guid activityId, Guid userId, CancellationToken ct = default);
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
    Task<IReadOnlyList<AssignedActivityResponse>> GetAssignedAsync(
        Guid userId,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<HouseholdMemberAssignmentResponse>> GetHouseholdAssignmentsAsync(
        Guid actingUserId,
        Guid eventId,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<ActivityRoleTypeResponse>> GetActivityRoleTypesAsync(
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

    Task<IReadOnlyList<AssignmentStatusTypeResponse>> GetAssignmentStatusTypesAsync(
        CancellationToken ct = default
    );
}

public interface IResourceService
{
    Task<IReadOnlyList<ResourceResponse>> ListAsync(CancellationToken ct = default);
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
    Task<IReadOnlyList<AnnouncementResponse>> ListAsync(CancellationToken ct = default);
    Task<Result<AnnouncementResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
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
    Task<IReadOnlyList<PartnerResponse>> ListAsync(CancellationToken ct = default);
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

public interface IResponseCacheService
{
    Task<object?> GetOrCreateAsync(
        string key,
        string group,
        TimeSpan ttl,
        Func<Task<object?>> factory
    );
    void InvalidateGroups(params string[] groups);
}
