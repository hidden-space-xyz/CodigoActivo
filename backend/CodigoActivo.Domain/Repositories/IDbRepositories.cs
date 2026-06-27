using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Domain.Repositories;

public interface IUserRepository : IDbRepository<User>
{
    Task<User?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<User>> ListWithDetailsAsync(CancellationToken ct = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByPhoneAsync(string phone, CancellationToken ct = default);

    Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId = null,
        CancellationToken ct = default
    );
    Task<bool> PhoneExistsAsync(
        string phone,
        Guid? excludeUserId = null,
        CancellationToken ct = default
    );

    Task<bool> HasTypeAssignmentAsync(Guid userId, Guid userTypeId, CancellationToken ct = default);
    Task AddTypeAssignmentAsync(UserTypeAssignment assignment, CancellationToken ct = default);

    Task<IReadOnlyList<User>> ListChildrenWithDetailsAsync(
        Guid parentId,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<UserTypeAssignment>> GetTypeAssignmentsAsync(
        Guid userId,
        CancellationToken ct = default
    );
    void RemoveTypeAssignment(UserTypeAssignment assignment);
}

public interface IEventRepository : IDbRepository<Event>
{
    Task<Event?> GetWithThumbnailAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Event>> ListAsync(
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken ct = default
    );

    Task<Event?> GetWithActivitiesAndAssignmentsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Marks the given event as featured and clears the flag on every other event.</summary>
    Task SetFeaturedAsync(Guid id, CancellationToken ct = default);
}

public interface IActivityRepository : IDbRepository<Activity>
{
    Task<IReadOnlyList<Activity>> ListByEventAsync(Guid eventId, CancellationToken ct = default);
    Task<Activity?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    Task<Activity?> GetForEditAsync(Guid id, CancellationToken ct = default);

    Task<bool> AllowedRoleExistsAsync(
        Guid activityId,
        Guid activityRoleTypeId,
        CancellationToken ct = default
    );

    Task<ActivityUserRoleAssignment?> GetAssignmentAsync(
        Guid userId,
        Guid activityId,
        CancellationToken ct = default
    );
    Task AddAssignmentAsync(ActivityUserRoleAssignment assignment, CancellationToken ct = default);
    void RemoveAssignment(ActivityUserRoleAssignment assignment);

    Task<IReadOnlyList<ActivityUserRoleAssignment>> GetUserAssignmentsAsync(
        Guid userId,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<ActivityUserRoleAssignment>> GetAssignmentsForUsersByEventAsync(
        IReadOnlyList<Guid> userIds,
        Guid eventId,
        CancellationToken ct = default
    );
}

public interface IResourceRepository : IDbRepository<Resource>;

public interface IAnnouncementRepository : IDbRepository<Announcement>
{
    /// <summary>Marks the given announcement as featured and clears the flag on every other one.</summary>
    Task SetFeaturedAsync(Guid id, CancellationToken ct = default);
}

public interface IPartnerRepository : IDbRepository<Partner>;

public interface IFileRepository : IDbRepository<FileEntity>;

public interface IUserTypeRepository : IDbRepository<UserType>;

public interface IUserStatusTypeRepository : IDbRepository<UserStatusType>;

public interface IActivityRoleTypeRepository : IDbRepository<ActivityRoleType>;

public interface IAssignmentStatusTypeRepository : IDbRepository<AssignmentStatusType>;
